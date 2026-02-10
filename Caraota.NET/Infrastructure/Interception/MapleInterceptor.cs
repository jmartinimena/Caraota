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
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Puerto inválido.");

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            _wrapper = WinDivertFactory.CreateForTcpPort(port);
            _wrapper.PacketReceived += OnHandshakeInit;
            _wrapper.Error += OnError;
            _wrapper.Start();

            _session = new MapleSession(_wrapper);
            _session.PacketDecrypted += OnPacketDecrypted;

            SessionMonitor.Start(_session);
        }

        private void OnHandshakeInit(WinDivertPacketViewEventArgs args)
        {
            SessionMonitor.LastPacketInterceptedTime = Environment.TickCount;

            if (!TcpHelper.TryExtractPayload(args.Packet,
                out Span<byte> payload))
                return;

            if (!HandleSessionState(args, payload))
                return;
        }

        private void OnPacketReceived(WinDivertPacketViewEventArgs args)
        {
            _sw.Restart();

            SessionMonitor.LastPacketInterceptedTime = Environment.TickCount;

            if (!TcpHelper.TryExtractPayload(args.Packet,
                out Span<byte> payload))
                return;

            if (payload.Length == 0)
                return;

            _session!.ProcessPacket(args, payload);
        }

        private void OnPacketDecrypted(MapleSessionViewEventArgs args)
        {
            HijackManager.ProcessQueue(ref args);

            var maplePacket = Pools.MaplePackets.Get();
            maplePacket.Initialize(args.MaplePacketView);

            if (!args.MaplePacketView.RequiresContinuation)
                PacketDispatcher.Dispatch(new MaplePacketEventArgs(maplePacket, args.Hijacked));

            if (!args.MaplePacketView.Rebuilt
                && !_session!.ProcessLeftovers(args))
                _session.EncryptAndSend(args);

            double ns = _sw.Elapsed.TotalNanoseconds;
            LogDiagnostic(ns);
        }

        private void OnError(Exception e)
        {
            _ = ErrorOcurred?.Invoke(e);
        }

        private bool HandleSessionState(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        {
            if (!_session!.Success)
            {
                var handshakeArgs = _session.Initialize(winDivertPacket, payload);

                _wrapper!.PacketReceived -= OnHandshakeInit;
                _wrapper!.PacketReceived += OnPacketReceived;

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
