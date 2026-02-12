using Caraota.Crypto.State;

namespace Caraota.NET.Infrastructure.TCP
{
    public interface IPacketReassembler
    {
        bool IsFragment(MaplePacketView packet);
        byte[] GetOrCreateBuffer(long id, int totalLength, bool isIncoming);
        byte[]? Finalize(long id, bool isIncoming);
    }

    public class PacketReassembler : IPacketReassembler
    {
        private readonly Dictionary<long, byte[]> _incomingBuffer = [];
        private readonly Dictionary<long, byte[]> _outgoingBuffer = [];

        public bool IsFragment(MaplePacketView packet)
        {
            var bufferMap = packet.IsIncoming ? _incomingBuffer : _outgoingBuffer;

            if (bufferMap.ContainsKey(packet.Id))
                return true;

            return packet.Leftovers.Length > 0;
        }

        public byte[] GetOrCreateBuffer(long id, int totalLength, bool isIncoming)
        {
            var bufferMap = isIncoming ? _incomingBuffer : _outgoingBuffer;

            if (!bufferMap.TryGetValue(id, out var outBuffer))
            {
                outBuffer = new byte[totalLength];
                bufferMap.Add(id, outBuffer);
            }
            return outBuffer;
        }

        public byte[]? Finalize(long id, bool isIncoming)
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
