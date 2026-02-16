using Caraota.NET.Common.Attributes;
using Caraota.NET.Common.Events;
using Caraota.NET.Common.Utils;
using Caraota.NET.Core.Models.Views;
using Caraota.NET.Core.Session;
using Caraota.NET.Infrastructure.TCP;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using static Caraota.NET.Infrastructure.Interception.PacketSide;

namespace Caraota.NET.Infrastructure.Interception
{
    public sealed class MapleInterceptor : IDisposable
    {
        public event Action<Exception>? ErrorOcurred;
        public event Action<HandshakePacketViewEventArgs>? HandshakeReceived;

        public readonly PacketSide Outgoing = new();
        public readonly PacketSide Incoming = new();

        private MapleSession _session = default!;
        private WinDivertWrapper _wrapper = default!;

        public void SetHandlers<T>() where T : class, new()
        {
            T handlerInstance = new();
            RegisterHandlers<OutgoingAttribute>(handlerInstance);
            RegisterHandlers<IncomingAttribute>(handlerInstance);
        }

        private void RegisterHandlers<TAttribute>(object target) where TAttribute : PacketHandlerAttribute
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var outgoingHandlers = target.GetType()
                .GetMethods(flags)
                .Select(m => new
                {
                    Method = m,
                    Attr = m.GetCustomAttribute<TAttribute>()
                })
                .Where(x => x.Attr != null);

            foreach (var h in outgoingHandlers)
            {
                try
                {
                    object? finalTarget = h.Method.IsStatic ? null : target;
                    var handlerDelegate = (PacketHandlerDelegate)Delegate.CreateDelegate(
                        typeof(PacketHandlerDelegate), finalTarget, h.Method);

                    Outgoing.Register(h.Attr!.Opcode, handlerDelegate);
                }
                catch (ArgumentException e)
                {
                    ErrorOcurred?.Invoke(e);
                }
            }
        }

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
            _wrapper.PacketReceived += OnPacketReceived;
            _wrapper.Start();

            _session = new MapleSession(_wrapper);
            _session.PacketDecrypted += OnPacketDecrypted;
            _session.Error += (e) => ErrorOcurred?.Invoke(e);
            _session.HandshakeReceived += (args) => HandshakeReceived?.Invoke(args);
        }

        private void OnPacketReceived(WinDivertPacketViewEventArgs args)
        {
            if (!TcpHelper.TryExtractPayload(args.Packet,
                out Span<byte> payload)) return;
            
            _session.ProcessPayload(args, payload);
        }

        private void OnPacketDecrypted(MapleSessionViewEventArgs args)
        {
            var maplePacketEventArgs = new MaplePacketEventArgs(args);
            var packetSide = args.MaplePacketView.IsIncoming ? Incoming : Outgoing;

            if (packetSide.TryGetFunc(args.MaplePacketView.Opcode, out var func))
                func?.Invoke(ref args.MaplePacketView);

            if (!args.MaplePacketView.RequiresContinuation)
                packetSide.Dispatch(maplePacketEventArgs);

            _session.ProcessDecrypted(args);
        }

        public void Dispose()
        {
            _wrapper.PacketReceived -= OnPacketReceived;

            _wrapper.Dispose();
            _session.Dispose();
            Outgoing.Dispose();
            Incoming.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
