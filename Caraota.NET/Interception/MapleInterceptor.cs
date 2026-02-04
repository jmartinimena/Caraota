using System.Diagnostics;
using System.Buffers.Binary;
using Caraota.NET.Events;
using Caraota.Crypto.Packets;
using Caraota.NET.Interception;
using Caraota.NET.Models;
using Caraota.Crypto.Processing;

namespace Caraota.NET.Interception
{
    public class MapleInterceptor : IDisposable
    {
        private readonly Stopwatch _sw = new();

        private MapleSession? _session;
        public ReadOnlyMemory<byte> ServerIV => _session!.ClientSend!.IV;
        public ReadOnlyMemory<byte> ClientIV => _session!.ServerSend!.IV;

        public delegate void MapleFinalPacketEventDelegate(MaplePacketEventArgs packet);
        public event EventHandler<HandshakePacketEventArgs>? OnHandshake;
        public event MapleFinalPacketEventDelegate? OnOutgoing;
        public event MapleFinalPacketEventDelegate? OnIncoming;
        public event EventHandler<Exception>? OnException;
        public event EventHandler? OnDisconnected;

        private WinDivertWrapper? _wrapper;
        private DateTime _lastPacketInterceptedTime;

        private readonly Queue<MaplePacket> _inHijackQueue = new();
        private readonly Queue<MaplePacket> _outHijackQueue = new();

        private readonly IDictionary<long, Memory<byte>> _outgoingBuffer = new Dictionary<long, Memory<byte>>();
        private readonly IDictionary<long, Memory<byte>> _incomingBuffer = new Dictionary<long, Memory<byte>>();

        private readonly Queue<MaplePacketEventArgs> _packetsQueue = new();

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

            var loggerThread = new Thread(ProcessLogQueue)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            loggerThread.Start();

            var checkAliveThread = new Thread(CheckAlive)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            checkAliveThread.Start();
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

        private void ProcessLogQueue()
        {
            while (true)
            {
                while (_packetsQueue.TryDequeue(out var args))
                {
                    if (args.Packet.IsIncoming)
                    {
                        OnIncoming?.Invoke(args);
                    }
                    else
                    {
                        OnOutgoing?.Invoke(args);
                    }
                }

                Thread.Sleep(100);
            }
        }

        private void ProcessRawPacket(WinDivertPacketEventArgs winDivertPacket, bool isIncoming)
        {
            _sw.Restart();
            _lastPacketInterceptedTime = DateTime.UtcNow;

            var tcpPacket = winDivertPacket.Packet;

            int ipHeaderLen = (tcpPacket[0] & 0x0F) << 2;
            int tcpHeaderOffset = ipHeaderLen;
            int tcpHeaderLen = ((tcpPacket[tcpHeaderOffset + 12] & 0xF0) >> 4) << 2;
            int payloadOffset = ipHeaderLen + tcpHeaderLen;
            int payloadLen = tcpPacket.Length - payloadOffset;

            if (payloadLen <= 0) return;

            ReadOnlySpan<byte> payload = tcpPacket.Slice(payloadOffset, payloadLen);

            if (!_session!.SessionSuccess)
            {
                _session.InitSession(winDivertPacket, payload);
                return;
            }

            var session = isIncoming ? _session.ServerRecv! : _session.ClientRecv!;

            ReadOnlySpan<byte> iv = session.IV.Span;
            var packet = PacketFactory.Parse(payload, iv, isIncoming);
            _session.DecryptPacket(winDivertPacket, packet, isIncoming);

            var ns = _sw.Elapsed.TotalNanoseconds;
            Console.WriteLine($"El interceptor tardo {ns} ns en procesar el paquete");
        }

        private void OnOutgoingMITM(MapleSessionEventArgs args)
        {
            ProcessHijackQueue(ref args, isIncoming: false);

            var maplePacket = new MaplePacket(args.DecodedPacket);
            var maplePacketEventArgs = new MaplePacketEventArgs(maplePacket, args.Hijacked);
            _packetsQueue.Enqueue(maplePacketEventArgs);

            if (!ProcessLeftovers(args, isIncoming: false))
            {
                EncryptAndSendPacketToServer(args);
            }
        }

        private void OnIncomingMITM(MapleSessionEventArgs args)
        {
            ProcessHijackQueue(ref args, isIncoming: true);

            var maplePacket = new MaplePacket(args.DecodedPacket);
            var maplePacketEventArgs = new MaplePacketEventArgs(maplePacket, args.Hijacked);
            _packetsQueue.Enqueue(maplePacketEventArgs);

            if (!ProcessLeftovers(args, isIncoming: true))
            {
                EncryptAndSendPacketToClient(args);
            }
        }

        private void OnHandshakeMITM(object? sender, HandshakePacketEventArgs e)
        {
            OnHandshake?.Invoke(sender, e);

            ModifyAndSend(e.MapleSessionEventArgs, e.Packet, isIncoming: true);
        }

        private static readonly byte[] HijackPattern = [.. "3A68696A61636B" // :hijack
            .Chunk(2).Select(s => Convert.ToByte(new string(s), 16))];
        private void ProcessHijackQueue(ref MapleSessionEventArgs args, bool isIncoming)
        {
            var hijackQueue = isIncoming ? _inHijackQueue : _outHijackQueue;
            if (hijackQueue.Count == 0) return;

            if (args.DecodedPacket.Opcode == (isIncoming ? 122 : 46) &&
                args.DecodedPacket.Data.IndexOf(HijackPattern) != -1)
            {
                args.DecodedPacket = PacketFactory.Parse(hijackQueue.Dequeue());
                args.Hijacked = true;
            }
        }

        private bool ProcessLeftovers(MapleSessionEventArgs args, bool isIncoming)
        {
            var buffer = isIncoming ? _incomingBuffer : _outgoingBuffer;
            bool hasBuffer = buffer.TryGetValue(args.DecodedPacket.Id, out var outBuffer);

            if (args.DecodedPacket.Leftovers.Length > 0 || hasBuffer)
            {
                if (!hasBuffer)
                {
                    outBuffer = new byte[args.DecodedPacket.TotalLength];
                    buffer.Add(args.DecodedPacket.Id, outBuffer);
                }

                var packet = args.DecodedPacket;
                if (TryEncryptPacket(ref packet, true))
                {
                    packet.Data.CopyTo(outBuffer.Span.Slice(args.DecodedPacket.ParentReaded, packet.Data.Length));
                }
                else
                {
                    args.DecodedPacket.Header.CopyTo(outBuffer.Span.Slice(args.DecodedPacket.ParentReaded, 4));
                    args.DecodedPacket.Payload.CopyTo(outBuffer.Span.Slice(args.DecodedPacket.ParentReaded + 4, args.DecodedPacket.Payload.Length));
                }

                if (args.DecodedPacket.Leftovers.Length == 0)
                {
                    ModifyAndSend(args, outBuffer.Span, true);
                    buffer.Remove(args.DecodedPacket.Id);
                }

                return true;
            }

            return false;
        }

        private void EncryptAndSendPacketToServer(MapleSessionEventArgs args)
        {
            if (_session?.ClientSend is null) return;

            var packet = args.DecodedPacket;

            if (TryEncryptPacket(ref packet, isIncoming: false))
            {
                ModifyAndSend(args, packet.Data, false);
            }
        }

        private void EncryptAndSendPacketToClient(MapleSessionEventArgs args)
        {
            if (_session!.ServerSend is null) return;

            var packet = args.DecodedPacket;

            if (TryEncryptPacket(ref packet, isIncoming: true))
            {
                ModifyAndSend(args, packet.Data, true);
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


        private ushort _fakeSeq = 0, _fakeAck = 0;
        private void ModifyAndSend(MapleSessionEventArgs args, ReadOnlySpan<byte> tcpPayload, bool isIncoming)
        {
            int ipH = (args.WinDivertPacket[0] & 0x0F) << 2;
            int tcpH = ((args.WinDivertPacket[ipH + 12] >> 4) & 0x0F) << 2;
            int totalHeader = ipH + tcpH;
            int totalSize = totalHeader + tcpPayload.Length;

            byte[] newTcpBuffer = new byte[totalSize]; // Idealmente usar Pool
            Span<byte> newTcpSpan = newTcpBuffer.AsSpan();

            args.WinDivertPacket[..totalHeader].CopyTo(newTcpSpan[..totalHeader]);
            tcpPayload.CopyTo(newTcpSpan[totalHeader..]);

            var delta = (ushort)Math.Abs(args.WinDivertPacket.Length - totalSize);
            uint finalSeq, finalAck;

            if (isIncoming)
            {
                finalSeq = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 4, 4)) + _fakeAck;
                finalAck = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 8, 4)) + _fakeSeq;
                _fakeAck += delta;
            }
            else
            {
                finalSeq = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 4, 4)) + _fakeSeq;
                finalAck = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 8, 4)) + _fakeAck;
                _fakeSeq += delta;
            }

            BinaryPrimitives.WriteUInt32BigEndian(newTcpSpan.Slice(ipH + 4, 4), finalSeq);
            BinaryPrimitives.WriteUInt32BigEndian(newTcpSpan.Slice(ipH + 8, 4), finalAck);
            BinaryPrimitives.WriteUInt16BigEndian(newTcpSpan.Slice(2, 2), (ushort)totalSize);

            ushort oldIpId = BinaryPrimitives.ReadUInt16BigEndian(newTcpSpan.Slice(4, 2));
            BinaryPrimitives.WriteUInt16BigEndian(newTcpSpan.Slice(4, 2), (ushort)(oldIpId + 1));

            _wrapper!.SendPacket(newTcpBuffer, args.Address);
        }

        public void HijackPacketOnServer(MaplePacket packet)
            => _outHijackQueue.Enqueue(packet);

        public void HijackPacketOnClient(MaplePacket packet)
            => _inHijackQueue.Enqueue(packet);

        public void CheckAlive()
        {
            while (true)
            {
                Thread.Sleep(800);

                if (_session == null || !_session.SessionSuccess)
                    continue;

                if ((DateTime.UtcNow - _lastPacketInterceptedTime).Seconds >= 8)
                {
                    OnDisconnected?.Invoke(this, new());

                    _session.SessionSuccess = false;

                    break;
                }
            }
        }

        public void Dispose() => _wrapper?.Dispose();
    }
}
