using System.Buffers;

namespace Caraota.NET.TCP
{
    public class PacketReassembler
    {
        private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

        private struct ReassemblyState
        {
            public byte[] Buffer;
            public int BytesWritten;
            public int TotalExpected;
        }

        private readonly Dictionary<long, ReassemblyState> _states = new(16);

        public bool Exists(long packetId) => _states.ContainsKey(packetId);
        public bool TryGetBuffer(long packetId, int totalLength, out Span<byte> buffer, out int currentOffset)
        {
            if (!_states.TryGetValue(packetId, out var state))
            {
                state = new ReassemblyState
                {
                    Buffer = _pool.Rent(totalLength),
                    BytesWritten = 0,
                    TotalExpected = totalLength
                };
                _states[packetId] = state;
            }

            buffer = state.Buffer.AsSpan(0, state.TotalExpected);
            currentOffset = state.BytesWritten;
            return true;
        }

        public void UpdateProgress(long packetId, int addedBytes)
        {
            if (_states.TryGetValue(packetId, out var state))
            {
                state.BytesWritten += addedBytes;
                _states[packetId] = state;
            }
        }

        public bool IsComplete(long packetId)
        {
            return _states.TryGetValue(packetId, out var state) && state.BytesWritten >= state.TotalExpected;
        }

        public void Release(long packetId)
        {
            if (_states.Remove(packetId, out var state))
            {
                _pool.Return(state.Buffer);
            }
        }
    }
}
