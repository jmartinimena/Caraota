namespace Caraota.NET.Protocol.Stream
{
    public class MapleStream
    {
        private readonly Dictionary<long, MapleBuffer> _starts = [];
        private readonly Dictionary<long, MapleBuffer> _payloads = [];

        public Span<byte> GetUnifiedPayload(long packetId, Span<byte> payload, out int continuationLength)
        {
            continuationLength = 0;

            if (_starts.TryGetValue(packetId, out var start))
            {
                int size = start.Length + payload.Length;
                var newPayload = new MapleBuffer(size);

                start.CopyTo(newPayload);
                payload.CopyTo(newPayload.AsSpan(start.Length));

                _payloads.Add(packetId, newPayload);

                continuationLength = start.Length;

                DisposeBuffer(packetId, _starts);

                return newPayload.AsSpan();
            }

            return payload;
        }

        public void SaveForContinuation(long packetId, Span<byte> data)
        {
            var start = new MapleBuffer(data.Length);
            data.CopyTo(start.AsSpan());

            _starts.Add(packetId + 1, start);
        }

        public void CleanPayload(long packetId)
        {
            DisposeBuffer(packetId, _payloads);
        }

        private static void DisposeBuffer(long packetId, Dictionary<long, MapleBuffer> bufferDictionary)
        {
            if (bufferDictionary.TryGetValue(packetId, out var buffer))
            {
                buffer.Dispose();
                bufferDictionary.Remove(packetId);
            }
        }
    }
}
