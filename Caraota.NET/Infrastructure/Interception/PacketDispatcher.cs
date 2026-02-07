using System.Threading.Channels;
using Caraota.NET.Common.Events;
using Caraota.NET.Common.Performance;

namespace Caraota.NET.Infrastructure.Interception
{
    public class PacketDispatcher
    {
        public event MaplePacketEventDelegate? OutgoingReceived;
        public event MaplePacketEventDelegate? IncomingReceived;
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

        public void Dispatch(MaplePacketEventArgs packet)
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
                        await IncomingReceived?.Invoke(args)!;
                    else
                        await OutgoingReceived?.Invoke(args)!;
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
