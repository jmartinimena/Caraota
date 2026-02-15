using Caraota.Crypto.State;
using System.Runtime.CompilerServices;

namespace Caraota.NET.Protocol.Stream
{
    public class MapleStream
    {
        private readonly Dictionary<long, MapleBuffer> _starts = [];
        private readonly Dictionary<long, MapleBuffer> _payloads = [];

        private readonly Dictionary<long, MapleBuffer> _incomingBuffer = [];
        private readonly Dictionary<long, MapleBuffer> _outgoingBuffer = [];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFragment(MaplePacketView packet)
        {
            var bufferMap = packet.IsIncoming ? _incomingBuffer : _outgoingBuffer;

            return bufferMap.ContainsKey(packet.Id) || packet.Leftovers.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapleBuffer? Finalize(long id, bool isIncoming)
        {
            var bufferMap = isIncoming ? _incomingBuffer : _outgoingBuffer;

            if (bufferMap.Remove(id, out var completedBuffer))
            {
                return completedBuffer;
            }

            return null;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
