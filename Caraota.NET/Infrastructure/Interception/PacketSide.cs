using Caraota.NET.Common.Events;

using System.Threading.Channels;

namespace Caraota.NET.Infrastructure.Interception
{
    public sealed class PacketSide : IDisposable
    {
        public event PacketReceivedDelegate? Received;

        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<ushort, PacketHandlerDelegate> _handlers = [];
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

        public bool Register(ushort opcode, PacketHandlerDelegate handler)
        {
            return _handlers.TryAdd(opcode, handler);
        }

        public bool Unregister(ushort opcode)
        {
            return _handlers.Remove(opcode);
        }

        internal bool TryGetHandler(ushort opcode, out PacketHandlerDelegate handler)
        {
            return _handlers.TryGetValue(opcode, out handler!);
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
            _cts.Dispose();

            _handlers.Clear();

            GC.SuppressFinalize(this);
        }
    }
}
