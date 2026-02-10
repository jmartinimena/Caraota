using System.Buffers;

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

        public unsafe MaplePacket(MaplePacketView maplePacket)
        {
            _timestamp = maplePacket.Id;
            _dataLen = maplePacket.Data.Length;
            _payloadLen = maplePacket.Payload.Length;

            Opcode = maplePacket.Opcode;
            IsIncoming = maplePacket.IsIncoming;

            int totalLen = _dataLen + _ivLen;
            _fullBuffer = ArrayPool<byte>.Shared.Rent(totalLen);

            var srcData = maplePacket.Data;
            var srcIV = maplePacket.IV;

            fixed (byte* pDest = _fullBuffer)
            {
                fixed (byte* pSrcData = srcData)
                {
                    Buffer.MemoryCopy(pSrcData, pDest, _dataLen, _dataLen);
                }

                fixed (byte* pSrcIV = srcIV)
                {
                    Buffer.MemoryCopy(pSrcIV, pDest + _dataLen, _ivLen, _ivLen);
                }
            }
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
