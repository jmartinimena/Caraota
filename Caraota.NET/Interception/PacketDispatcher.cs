using System.Threading.Channels;

using Caraota.NET.Events;
using Caraota.NET.Performance;

namespace Caraota.NET.Interception
{
    public class PacketDispatcher
    {
        public event MaplePacketEventDelegate? OnOutgoing;
        public event MaplePacketEventDelegate? OnIncoming;
        public delegate Task MaplePacketEventDelegate(MaplePacketEventArgs packet);

        private readonly Channel<MaplePacketEventArgs> _channel = Channel.CreateBounded<MaplePacketEventArgs>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        public PacketDispatcher()
        {
            Task.Run(ProcessLogQueueAsync);
        }

        public void Enqueue(MaplePacketEventArgs packet)
        {
            _channel.Writer.TryWrite(packet);
        }

        private async Task ProcessLogQueueAsync()
        {
            await foreach (var args in _channel.Reader.ReadAllAsync())
            {
                try
                {
                    if (args.Packet.IsIncoming)
                        await OnIncoming?.Invoke(args)!;
                    else
                        await OnOutgoing?.Invoke(args)!;
                }
                finally
                {
                    args.Packet.Dispose();
                    Pools.MaplePackets.Return(args.Packet);
                }
            }
        }
    }
}
