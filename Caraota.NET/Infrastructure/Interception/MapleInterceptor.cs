using System.Runtime;
using System.Diagnostics;

using Caraota.NET.Engine.Logic;
using Caraota.NET.Engine.Session;
using Caraota.NET.Engine.Monitoring;

using Caraota.NET.Common.Utils;
using Caraota.NET.Common.Events;
using Caraota.NET.Common.Performance;

namespace Caraota.NET.Infrastructure.Interception
{
    public sealed class MapleInterceptor : IDisposable
    {
        public event Func<Exception, Task>? ErrorOcurred;
        public event Func<HandshakeEventArgs, Task>? HandshakeReceived;

        public readonly HijackManager HijackManager = new();
        public readonly PacketDispatcher PacketDispatcher = new();
        public readonly MapleSessionMonitor SessionMonitor = new(); 

        private MapleSession? _session;
        private WinDivertWrapper? _wrapper;

        private readonly Stopwatch _sw = new();

        public void StartListening(int port)
        {
            if(port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Puerto inválido.");

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            _wrapper = WinDivertFactory.CreateForTcpPort(port);
            _wrapper.Error += OnError;
            _wrapper.PacketReceived += OnPacketReceived;
            _wrapper.Start();

            _session = new MapleSession(_wrapper);
            _session.PacketDecrypted += OnPacketDecrypted;
        }

        private void OnPacketReceived(WinDivertPacketEventArgs args)
        {
            //_sw.Restart();

            if (!TcpHelper.TryExtractPayload(args.Packet,
                out ReadOnlySpan<byte> payload))
                return;

            if (!HandleSessionState(args, payload))
                return;

            _session!.Decrypt(args, payload);
        }

        private void OnPacketDecrypted(MapleSessionPacket args)
        {
            HijackManager.ProcessQueue(ref args);

            var maplePacket = Pools.MaplePackets.Get();
            maplePacket.Initialize(args.DecodedPacket);

            PacketDispatcher.Dispatch(new MaplePacketEventArgs(maplePacket, args.Hijacked));

            if (!_session!.ProcessLeftovers(args))
                _session.EncryptAndSend(args);

            //double ns = _sw.Elapsed.TotalNanoseconds;
            //LogDiagnostic(ns);
        }

        private void OnError(Exception e)
        {
            _ = ErrorOcurred?.Invoke(e);
        }

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
