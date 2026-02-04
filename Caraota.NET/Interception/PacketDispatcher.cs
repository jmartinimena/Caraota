using Caraota.NET.Events;

namespace Caraota.NET.Interception
{
    public class PacketDispatcher
    {
        public event MaplePacketEventDelegate? OnOutgoing;
        public event MaplePacketEventDelegate? OnIncoming;
        public delegate void MaplePacketEventDelegate(MaplePacketEventArgs packet);

        private readonly Queue<MaplePacketEventArgs> _packetsQueue = new();

        public PacketDispatcher()
        {
            var loggerThread = new Thread(ProcessLogQueue)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            loggerThread.Start();
        }

        public void Enqueue(MaplePacketEventArgs packet) => _packetsQueue.Enqueue(packet);

        private void ProcessLogQueue()
        {
            while (true)
            {
                while (_packetsQueue.TryDequeue(out var args))
                {
                    if (args.Packet.IsIncoming)
                    {
                        OnIncoming?.Invoke(args);
                    }
                    else
                    {
                        OnOutgoing?.Invoke(args);
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}
