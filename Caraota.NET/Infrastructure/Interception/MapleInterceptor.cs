using System.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Caraota.NET.Engine.Logic;
using Caraota.NET.Engine.Session;
using Caraota.NET.Engine.Monitoring;
using Caraota.NET.Common.Performance;
using Caraota.NET.Common.Events;
using Caraota.NET.Common.Utils;

namespace Caraota.NET.Infrastructure.Interception
{
    public class MapleInterceptor : IDisposable
    {
        public delegate Task MapleInterceptorAsyncEventDelegate<T>(T packet);
        public event MapleInterceptorAsyncEventDelegate<Exception>? ErrorOcurred;
        public event MapleInterceptorAsyncEventDelegate<HandshakeEventArgs>? HandshakeReceived;

        public readonly HijackManager HijackManager = new();
        public readonly PacketDispatcher PacketDispatcher = new();
        public readonly MapleSessionMonitor SessionMonitor = new();

        private MapleSession? _session;
        private WinDivertWrapper? _wrapper;

        private readonly Stopwatch _sw = new();

        public void StartListening(int port)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            _wrapper = WinDivertFactory.CreateForTcpPort(port);
            _wrapper.Error += OnError;
            _wrapper.PacketReceived += OnPacketReceived;
            _wrapper.Start();

            _session = new MapleSession(_wrapper);
            _session.PacketDecrypted += OnPacketDecrypted;

            // Algunos servidores mantienen pings constantes mientras que otros no
            // Se debe buscar una forma diferente de detectar desconexion
            //SessionMonitor.Start(_session);
        }

        private void OnError(Exception e)
        {
            _ = ErrorOcurred?.Invoke(e);
        }

        private void OnPacketReceived(WinDivertPacketEventArgs args)
        {
            ProcessRawPacket(args);
        }

        private void ProcessRawPacket(WinDivertPacketEventArgs winDivertPacket)
        {
            _sw.Restart();

            SessionMonitor!.LastPacketInterceptedTime = Environment.TickCount64;

            if (!Tcp.TryExtractPayload(winDivertPacket.Packet, out ReadOnlySpan<byte> payload))
            {
                return;
            }

            if (!HandleSessionState(winDivertPacket, payload))
            {
                return;
            }

            _session!.Decrypt(winDivertPacket, payload);

            double ns = _sw.Elapsed.TotalNanoseconds;
            LogDiagnostic(ns);
        }

        private void OnPacketDecrypted(MapleSessionPacket args)
        {
            HijackManager.ProcessQueue(ref args);

            var maplePacket = Pools.MaplePackets.Get();
            maplePacket.Initialize(args.DecodedPacket);

            PacketDispatcher.Enqueue(new MaplePacketEventArgs(maplePacket, args.Hijacked));

            if (!_session!.ProcessLeftovers(args))
                _session!.EncryptAndSend(args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HandleSessionState(WinDivertPacketEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        {
            if (!_session!.IsInitialized())
            {
                var handshakeArgs = _session.Initialize(winDivertPacket, payload);

                HandshakeReceived?.Invoke(new HandshakeEventArgs(handshakeArgs));

                return false;
            }

            return true;
        }

        [Conditional("DEBUG")]
        private static void LogDiagnostic(double nanoseconds) 
            => Debug.WriteLine($"[Interceptor] Cycle: {nanoseconds} ns");

        public void Dispose()
        {
            _wrapper?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
