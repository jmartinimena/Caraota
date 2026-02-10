using System.Runtime;
using System.Diagnostics;

using Caraota.NET.Engine.Logic;
using Caraota.NET.Engine.Session;
using Caraota.NET.Engine.Monitoring;

using Caraota.NET.Common.Utils;
using Caraota.NET.Common.Events;

namespace Caraota.NET.Infrastructure.Interception
{
    public sealed class MapleInterceptor : IDisposable
    {
        public event Action<Exception>? ErrorOcurred;
        public event Action<HandshakeEventArgs>? HandshakeReceived;

        public readonly PacketSide Outgoing = new();
        public readonly PacketSide Incoming = new();
        public readonly HijackManager HijackManager = new();
        public readonly MapleSessionMonitor SessionMonitor = new();

        private MapleSession _session = default!;
        private WinDivertWrapper _wrapper = default!;

        private readonly Stopwatch _sw = new();

        public void StartListening(int port)
        {
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Puerto inválido.");

            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            var currentProcess = Process.GetCurrentProcess();
            currentProcess.PriorityClass = ProcessPriorityClass.RealTime;

            _wrapper = WinDivertFactory.CreateForTcpPort(port);
            _wrapper.Error += (e) => ErrorOcurred?.Invoke(e);
            _wrapper.PacketReceived += OnHandshakeInit;
            _wrapper.Start();

            _session = new MapleSession(_wrapper);
            _session.PacketDecrypted += OnPacketDecrypted;

            SessionMonitor.Start(_session);
        }

        private void OnHandshakeInit(WinDivertPacketViewEventArgs args)
        {
            SessionMonitor.LastPacketInterceptedTime = Stopwatch.GetTimestamp();

            if (!TcpHelper.TryExtractPayload(args.Packet,
                out Span<byte> payload)) return;

            InitializeSession(args, payload);
        }

        private void OnPacketReceived(WinDivertPacketViewEventArgs args)
        {
            _sw.Restart();

            SessionMonitor.LastPacketInterceptedTime = Stopwatch.GetTimestamp();

            if (!TcpHelper.TryExtractPayload(args.Packet,
                out Span<byte> payload)) return;

            _session.ProcessRaw(args, payload);
        }

        private void OnPacketDecrypted(MapleSessionViewEventArgs args)
        {
            if (!args.MaplePacketView.RequiresContinuation)
            {
                HijackManager.ProcessQueue(ref args);

                var maplePacketEventArgs = new MaplePacketEventArgs(args);
                var packetSide = args.MaplePacketView.IsIncoming ? Incoming : Outgoing;

                if (packetSide.TryGetFunc(args.MaplePacketView.Opcode, out var func))
                    func?.Invoke(maplePacketEventArgs);

                packetSide.Dispatch(maplePacketEventArgs);
            }

            if (!args.MaplePacketView.Rebuilt)
                _session.ProcessDecrypted(args);

            double ns = _sw.Elapsed.TotalNanoseconds;
            LogDiagnostic(ns);
        }

        private void InitializeSession(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        {
            var handshakeArgs = _session.Initialize(winDivertPacket, payload);

            _wrapper.PacketReceived -= OnHandshakeInit;
            _wrapper.PacketReceived += OnPacketReceived;

            HandshakeReceived?.Invoke(new HandshakeEventArgs(handshakeArgs));
        }

        [Conditional("DEBUG")]
        private static void LogDiagnostic(double nanoseconds)
            => Debug.WriteLine($"[Interceptor] Cycle: {nanoseconds} ns");

        public void Dispose()
        {
            _wrapper.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
