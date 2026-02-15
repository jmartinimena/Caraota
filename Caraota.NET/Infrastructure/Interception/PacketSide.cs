using System.Threading.Channels;

using Caraota.NET.Common.Events;

namespace Caraota.NET.Infrastructure.Interception
{
    public sealed class PacketSide : IDisposable
    {
        public event Action<MaplePacketEventArgs>? Received;

        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<ushort, Action<MaplePacketEventArgs>> _handlers = [];
        private readonly Channel<MaplePacketEventArgs> _channel = Channel.CreateBounded<MaplePacketEventArgs>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        public PacketSide()
        {
            Task.Run(() => ProcessLogQueueAsync(_cts.Token));
        }

        private async Task ProcessLogQueueAsync(CancellationToken ct)
        {
            await foreach (var args in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    Received?.Invoke(args);
                }
                finally
                {
                    args.Packet.Dispose();
                }
            }

            CleanupRemainingPackets();
        }

        internal void Dispatch(MaplePacketEventArgs packet)
        {
            _channel.Writer.TryWrite(packet);
        }

        public bool Register(ushort opcode, Action<MaplePacketEventArgs> action)
        {
            return _handlers.TryAdd(opcode, action);
        }

        public bool Unregister(ushort opcode)
        {
            return _handlers.Remove(opcode);
        }

        internal bool TryGetFunc(ushort opcode, out Action<MaplePacketEventArgs> action)
        {
            return _handlers.TryGetValue(opcode, out action!);
        }

        private void CleanupRemainingPackets()
        {
            while (_channel.Reader.TryRead(out var args))
            {
                args.Packet.Dispose();
            }
        }

        public void Dispose()
        {
            _channel.Writer.TryComplete();
            _cts.Cancel();
            _handlers.Clear();
            _cts.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
