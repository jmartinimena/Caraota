using System.Buffers;

using Caraota.Crypto.State;
using Caraota.Crypto.Utils;

using Caraota.NET.Common.IO;

namespace Caraota.NET.Protocol.Structures
{
    public readonly struct MaplePacket : IDisposable
    {
        private readonly byte[]? _fullBuffer;
        private const int _ivLen = 4;
        private const int _headerLen = 4;
        private readonly int _dataLen;
        private readonly int _payloadLen;

        public readonly long Timestamp;
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
        public readonly string FormattedTime => PacketUtils.GetRealTime(Timestamp).ToString("HH:mm:ss:fff");

        public unsafe MaplePacket(MaplePacketView maplePacket)
        {
            Timestamp = maplePacket.Timestamp;
            _dataLen = maplePacket.Data.Length;
            _payloadLen = maplePacket.Payload.Length;

            Opcode = maplePacket.Opcode;
            IsIncoming = maplePacket.IsIncoming;

            int totalLen = _dataLen + _ivLen;
            _fullBuffer = ArrayPool<byte>.Shared.Rent(totalLen);

            var destSpan = _fullBuffer.AsSpan(0, totalLen);
            maplePacket.Data.CopyTo(destSpan);
            maplePacket.IV.CopyTo(destSpan[_dataLen..]);
        }

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
