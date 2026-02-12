using System.Runtime;
using System.Diagnostics;

using Caraota.NET.Engine.Logic;
using Caraota.NET.Engine.Session;
using Caraota.NET.Engine.Monitoring;

using Caraota.NET.Common.Utils;
using Caraota.NET.Common.Events;
using Caraota.NET.Infrastructure.TCP;

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
            var wrapper = WinDivertFactory.CreateForTcp(port);
            StartListening(wrapper);
        }

        public void StartListening(PortRange portRange)
        {
            var wrapper = WinDivertFactory.CreateForTcp(portRange);
            StartListening(wrapper);
        }

        private void StartListening(WinDivertWrapper wrapper)
        {
            var currentProcess = Process.GetCurrentProcess();
            currentProcess.PriorityClass = ProcessPriorityClass.RealTime;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            _wrapper = wrapper;
            _wrapper.Error += (e) => ErrorOcurred?.Invoke(e);
            _wrapper.PacketReceived += OnHandshakeInit;
            _wrapper.Start();

            _session = new MapleSession(_wrapper);
            _session.PacketDecrypted += OnPacketDecrypted;
            _session.HandshakeReceived += OnHandshakeReceived;

            SessionMonitor.Start(_session);
        }

        private void OnHandshakeReceived(HandshakeEventArgs args)
        {
            HandshakeReceived?.Invoke(args);
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

            //Console.WriteLine($"Original: {Convert.ToHexString(args.Packet)}");

            _session.ProcessRaw(args, payload);
        }

        private void OnPacketDecrypted(MapleSessionViewEventArgs args)
        {
            HijackManager.ProcessQueue(ref args);

            var maplePacketEventArgs = new MaplePacketEventArgs(args);
            var packetSide = args.MaplePacketView.IsIncoming ? Incoming : Outgoing;

            if (packetSide.TryGetFunc(args.MaplePacketView.Opcode, out var func))
                func?.Invoke(maplePacketEventArgs);

            if (!args.MaplePacketView.RequiresContinuation)
                packetSide.Dispatch(maplePacketEventArgs);

            _session.ProcessDecrypted(args);

            double ns = _sw.Elapsed.TotalNanoseconds;
            LogDiagnostic(ns);
        }

        private void InitializeSession(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        {
            if(_session.Initialize(winDivertPacket, payload, out var handshakePacketView))
            {
                _wrapper.PacketReceived -= OnHandshakeInit;
                _wrapper.PacketReceived += OnPacketReceived;

                HandshakeReceived?.Invoke(new HandshakeEventArgs(handshakePacketView));
            }
        }

        [Conditional("DEBUG")]
        private static void LogDiagnostic(double nanoseconds)
            => Debug.WriteLine($"[Interceptor] Cycle: {nanoseconds} ns");

        public void Dispose()
        {
            _wrapper.PacketReceived -= OnPacketReceived;

            _wrapper.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
