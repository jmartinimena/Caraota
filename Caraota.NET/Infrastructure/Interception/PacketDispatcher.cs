using System.Threading.Channels;

using Caraota.NET.Common.Events;

namespace Caraota.NET.Infrastructure.Interception
{
    public class PacketDispatcher
    {
        public event Func<MaplePacketEventArgs, Task>? OutgoingReceived;
        public event Func<MaplePacketEventArgs, Task>? IncomingReceived;
        

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
                    {
                        await IncomingReceived?.Invoke(args)!;

                        
                    }
                    else
                    {
                        await OutgoingReceived?.Invoke(args)!;

                        
                    }
                }
                finally
                {
                    args.Packet.Dispose();
                }
            }
        }
    }
}
