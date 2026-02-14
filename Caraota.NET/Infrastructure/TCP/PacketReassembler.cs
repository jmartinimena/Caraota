using Caraota.Crypto.State;
using Caraota.NET.Protocol.Stream;
using System.Runtime.CompilerServices;

namespace Caraota.NET.Infrastructure.TCP
{
    public class PacketReassembler
    {
        private readonly Dictionary<long, MapleBuffer> _incomingBuffer = [];
        private readonly Dictionary<long, MapleBuffer> _outgoingBuffer = [];

        public bool IsFragment(MaplePacketView packet)
        {
            var bufferMap = packet.IsIncoming ? _incomingBuffer : _outgoingBuffer;

            return bufferMap.ContainsKey(packet.Id) || packet.Leftovers.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public MapleBuffer GetOrCreateBuffer(long id, int totalLength, bool isIncoming)
        {
            var bufferMap = isIncoming ? _incomingBuffer : _outgoingBuffer;

            if (!bufferMap.TryGetValue(id, out var mapleBuffer))
            {
                mapleBuffer = new MapleBuffer(totalLength);
                bufferMap.Add(id, mapleBuffer);
            }

            return mapleBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public MapleBuffer? Finalize(long id, bool isIncoming)
        {
            var bufferMap = isIncoming ? _incomingBuffer : _outgoingBuffer;

            if (bufferMap.Remove(id, out var completedBuffer))
            {
                return completedBuffer;
            }

            return null;
        }
    }
}
