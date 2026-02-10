using System.Threading.Channels;

using Caraota.NET.Common.Events;

namespace Caraota.NET.Infrastructure.Interception
{
    public sealed class PacketSide
    {
        public event Func<MaplePacketEventArgs, Task>? Received;

        private readonly Dictionary<ushort, Func<MaplePacketEventArgs, Task>> _handlers = [];
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
                    await Received?.Invoke(args)!;
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

        public bool Register(ushort opcode, Func<MaplePacketEventArgs, Task> func)
        {
            return _handlers.TryAdd(opcode, func);
        }

        public bool Unregister(ushort opcode)
        {
            return _handlers.Remove(opcode);
        }

        internal bool TryGetFunc(ushort opcode, out Func<MaplePacketEventArgs, Task> func)
        {
            return _handlers.TryGetValue(opcode, out func!);
        }
    }
}
