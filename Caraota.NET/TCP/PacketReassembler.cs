namespace Caraota.NET.TCP
{
    public class PacketReassembler
    {
        private readonly Dictionary<long, byte[]> _incomingBuffer = new();
        private readonly Dictionary<long, byte[]> _outgoingBuffer = new();

        public bool IsFragment(long id, int leftoversLength, bool isIncoming)
        {
            var bufferMap = isIncoming ? _incomingBuffer : _outgoingBuffer;
            return leftoversLength > 0 || bufferMap.ContainsKey(id);
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
