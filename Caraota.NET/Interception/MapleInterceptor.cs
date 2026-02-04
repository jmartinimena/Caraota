using System.Diagnostics;
using System.Runtime.CompilerServices;

using Caraota.NET.TCP;
using Caraota.NET.Events;
using Caraota.NET.Models;

using Caraota.Crypto.Packets;
using Caraota.Crypto.Processing;

namespace Caraota.NET.Interception
{
    public class MapleInterceptor : IDisposable
    {
        public event EventHandler<Exception>? OnException;
        public event EventHandler<HandshakePacketEventArgs>? OnHandshake;

        public readonly HijackManager HijackManager = new();
        public readonly PacketDispatcher PacketDispatcher = new();
        public readonly MapleSessionMonitor SessionMonitor = new();

        private MapleSession? _session;
        private WinDivertWrapper? _wrapper;
        private TcpStackArchitect? _tcpStack;

        private readonly Stopwatch _sw = new();
        private readonly PacketReassembler _reassembler = new();

        public void StartListening(int port)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            _session = new();
            _session.OnHandshake += OnHandshakeMITM;
            _session.OnOutgoingPacket += OnOutgoingMITM;
            _session.OnIncomingPacket += OnIncomingMITM;

            _wrapper = WinDivertFactory.CreateForTcpPort(port);
            _wrapper.OnError += Wrapper_OnError;
            _wrapper.OnInboundPacket += Wrapper_OnInboundPacket;
            _wrapper.OnOutboundPacket += Wrapper_OnOutboundPacket;
            _wrapper.Start();

            _tcpStack = new(_wrapper);

            SessionMonitor.Start(_session);
        }

        private void Wrapper_OnOutboundPacket(WinDivertPacketEventArgs args)
        {
            ProcessRawPacket(args, false);
        }

        private void Wrapper_OnInboundPacket(WinDivertPacketEventArgs args)
        {
            ProcessRawPacket(args, true);
        }

        private void Wrapper_OnError(Exception e)
        {
            OnException?.Invoke(this, e);
        }

        private void ProcessRawPacket(WinDivertPacketEventArgs winDivertPacket, bool isIncoming)
        {
            _sw.Restart();

            SessionMonitor!.LastPacketInterceptedTime = Environment.TickCount64;

            if (!TryExtractPayload(winDivertPacket.Packet, out ReadOnlySpan<byte> payload))
            {
                return;
            }

            if (!HandleSessionState(winDivertPacket, payload))
            {
                return;
            }

            ProcessMaplePacket(winDivertPacket, payload, isIncoming);

            double ns = _sw.Elapsed.TotalNanoseconds;
            LogDiagnostic(ns);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryExtractPayload(ReadOnlySpan<byte> tcpPacket, out ReadOnlySpan<byte> payload)
        {
            int ipH = (tcpPacket[0] & 0x0F) << 2;
            int tcpH = ((tcpPacket[ipH + 12] & 0xF0) >> 4) << 2;

            int offset = ipH + tcpH;
            int len = tcpPacket.Length - offset;

            if (len <= 0) { payload = default; return false; }

            payload = tcpPacket.Slice(offset, len);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HandleSessionState(WinDivertPacketEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        {
            if (!_session!.SessionSuccess)
            {
                Debug.WriteLine($"[Session] Intentando sincronizar Handshake. Payload Len: {payload.Length}");
                _session.InitSession(winDivertPacket, payload);
                return false;
            }
            return true;
        }

        private void ProcessMaplePacket(WinDivertPacketEventArgs winDivertPacket, ReadOnlySpan<byte> payload, bool isIncoming)
        {
            var cryptoSession = isIncoming ? _session!.ServerRecv! : _session!.ClientRecv!;

            var packet = PacketFactory.Parse(payload, cryptoSession.IV.Span, isIncoming);

            _session.DecryptPacket(winDivertPacket, packet, isIncoming);
        }

        [Conditional("DEBUG")]
        private void LogDiagnostic(double nanoseconds)
        {
            Debug.WriteLine($"[Interceptor] Cycle: {nanoseconds} ns");
        }

        private void OnOutgoingMITM(MapleSessionEventArgs args)
        {
            HijackManager.ProcessQueue(ref args, isIncoming: false);

            var maplePacket = new MaplePacket(args.DecodedPacket);
            var maplePacketEventArgs = new MaplePacketEventArgs(maplePacket, args.Hijacked);
            PacketDispatcher.Enqueue(maplePacketEventArgs);

            if (!ProcessLeftovers(args, isIncoming: false))
            {
                EncryptAndSendPacketToServer(args);
            }
        }

        private void OnIncomingMITM(MapleSessionEventArgs args)
        {
            HijackManager.ProcessQueue(ref args, isIncoming: true);

            var maplePacket = new MaplePacket(args.DecodedPacket);
            var maplePacketEventArgs = new MaplePacketEventArgs(maplePacket, args.Hijacked);
            PacketDispatcher.Enqueue(maplePacketEventArgs);

            if (!ProcessLeftovers(args, isIncoming: true))
            {
                EncryptAndSendPacketToClient(args);
            }
        }

        private void OnHandshakeMITM(object? sender, HandshakePacketEventArgs e)
        {
            OnHandshake?.Invoke(sender, e);

            _tcpStack!.ModifyAndSend(e.MapleSessionEventArgs, e.Packet, isIncoming: true);
        }

        private bool ProcessLeftovers(MapleSessionEventArgs args, bool isIncoming)
        {
            var packet = args.DecodedPacket;
            long id = packet.Id;

            bool hasBuffer = _reassembler.Exists(id);
            if (packet.Leftovers.Length == 0 && !hasBuffer) return false;

            if (!_reassembler.TryGetBuffer(id, packet.TotalLength, out Span<byte> outBuffer, out int offset))
                return false;

            var currentFragment = packet;

            if (TryEncryptPacket(ref currentFragment, isIncoming))
            {
                currentFragment.Data.CopyTo(outBuffer.Slice(packet.ParentReaded, currentFragment.Data.Length));
            }
            else
            {
                packet.Header.CopyTo(outBuffer.Slice(packet.ParentReaded, 4));
                packet.Payload.CopyTo(outBuffer.Slice(packet.ParentReaded + 4, packet.Payload.Length));
            }

            _reassembler.UpdateProgress(id, packet.Data.Length);

            if (packet.Leftovers.Length == 0)
            {
                _tcpStack!.ModifyAndSend(args, outBuffer, isIncoming);
                _reassembler.Release(id);
            }

            return true;
        }

        private void EncryptAndSendPacketToServer(MapleSessionEventArgs args)
        {
            if (_session?.ClientSend is null) return;

            var packet = args.DecodedPacket;

            if (TryEncryptPacket(ref packet, isIncoming: false))
            {
                _tcpStack!.ModifyAndSend(args, packet.Data, false);
            }
        }

        private void EncryptAndSendPacketToClient(MapleSessionEventArgs args)
        {
            if (_session!.ServerSend is null) return;

            var packet = args.DecodedPacket;

            if (TryEncryptPacket(ref packet, isIncoming: true))
            {
                _tcpStack!.ModifyAndSend(args, packet.Data, true);
            }
        }

        private bool TryEncryptPacket(ref DecodedPacket packet, bool isIncoming)
        {
            var crypto = isIncoming ? _session!.ServerSend : _session!.ClientSend;

            if (crypto is null) return false;

            bool success;
            if (success = crypto.Validate(packet, isIncoming))
                crypto.Encrypt(ref packet);

            return success;
        }

        public void Dispose() => _wrapper?.Dispose();
    }
}
