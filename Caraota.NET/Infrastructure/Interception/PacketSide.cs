using System.Threading.Channels;

using Caraota.NET.Common.Events;

namespace Caraota.NET.Infrastructure.Interception
{
    public sealed class PacketSide
    {
        public event Action<MaplePacketEventArgs>? Received;

        private readonly Dictionary<ushort, Action<MaplePacketEventArgs>> _handlers = [];
        private readonly Channel<MaplePacketEventArgs> _channel = Channel.CreateBounded<MaplePacketEventArgs>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        public PacketSide()
        {
            Task.Run(ProcessLogQueueAsync);
        }

        private async Task ProcessLogQueueAsync()
        {
            await foreach (var args in _channel.Reader.ReadAllAsync())
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
    }
}
