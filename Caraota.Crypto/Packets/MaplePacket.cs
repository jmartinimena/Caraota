using System.Buffers;
using System.Buffers.Binary;

namespace Caraota.Crypto.Packets
{
    public readonly struct MaplePacket : IDisposable
    {
        private readonly long _timestamp;
        private readonly byte[]? _fullBuffer;
        private const int _ivLen = 4;
        private const int _headerLen = 4;
        private readonly int _dataLen;
        private readonly int _payloadLen;

        public readonly ushort Opcode;
        public readonly bool IsIncoming;

        public readonly Memory<byte> Data => _fullBuffer.AsMemory(0, _dataLen);
        public readonly ReadOnlyMemory<byte> IV => _fullBuffer.AsMemory(_dataLen, _ivLen);
        public readonly ReadOnlyMemory<byte> Header => _fullBuffer.AsMemory(0, _headerLen);
        public readonly ReadOnlyMemory<byte> Payload => _fullBuffer.AsMemory(_headerLen, _payloadLen);
        public readonly string IVStr => Convert.ToHexString(IV.Span);
        public readonly string HeaderStr => Convert.ToHexString(Header.Span);
        public readonly string PayloadStr => Convert.ToHexString(Payload.Span);
        public readonly string ToHexString() => Convert.ToHexString(Data.Span);
        public readonly string FormattedTime => PacketUtils.GetRealTime(_timestamp).ToString("HH:mm:ss:fff");

        public MaplePacket(MaplePacketView maplePacket)
        {
            _dataLen = maplePacket.Data.Length;
            _payloadLen = maplePacket.Payload.Length;

            int totalLen = _dataLen + _ivLen;

            _fullBuffer = ArrayPool<byte>.Shared.Rent(totalLen);
            maplePacket.Data.CopyTo(_fullBuffer.AsSpan(0, _dataLen));
            maplePacket.IV.CopyTo(_fullBuffer.AsSpan(_dataLen, _ivLen));

            IsIncoming = maplePacket.IsIncoming;
            _timestamp = maplePacket.Id;
            Opcode = maplePacket.Opcode;
        }

        public readonly string Predict() => PacketUtils.Predict(Payload);

        public MaplePacketReader GetReader() => new(this);

        public void Dispose()
        {
            if (_fullBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_fullBuffer);
            }

            GC.SuppressFinalize(this);
        }
    }
}
