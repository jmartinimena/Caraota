using System.Buffers;
using System.Buffers.Binary;

namespace Caraota.Crypto.Packets
{
    public class MaplePacket : IDisposable
    {
        private long _timestamp;
        private byte[]? _fullBuffer;

        public int DataLen { get; private set; }
        public const int IvLen = 4;
        public const int HeaderLen = 4;
        public int PayloadLen { get; private set; }

        public Memory<byte> Data => _fullBuffer.AsMemory(0, DataLen);
        public ReadOnlyMemory<byte> IV => _fullBuffer.AsMemory(DataLen, IvLen);
        public ReadOnlyMemory<byte> Header => _fullBuffer.AsMemory(0, HeaderLen);
        public ReadOnlyMemory<byte> Payload => _fullBuffer.AsMemory(HeaderLen, PayloadLen);

        public ushort Opcode { get; private set; }
        public bool IsIncoming { get; private set; }

        public string IVStr => Convert.ToHexString(IV.Span);
        public string HeaderStr => Convert.ToHexString(Header.Span);
        public string PayloadStr => Convert.ToHexString(Payload.Span);
        public string ToHexString() => Convert.ToHexString(Data.Span);
        public string FormattedTime => PacketUtils.GetRealTime(_timestamp).ToString("HH:mm:ss:fff");

        // Constructor sin parametros para el ObjectPool
        public MaplePacket() { }

        public MaplePacket(MaplePacketView maplePacket) => Initialize(maplePacket);

        public void Initialize(MaplePacketView maplePacket)
        {
            DataLen = maplePacket.Data.Length;
            PayloadLen = maplePacket.Payload.Length;

            int totalNeeded = DataLen + IvLen;

            _fullBuffer = ArrayPool<byte>.Shared.Rent(totalNeeded);

            maplePacket.Data.CopyTo(_fullBuffer.AsSpan(0, DataLen));
            maplePacket.IV.CopyTo(_fullBuffer.AsSpan(DataLen, IvLen));

            IsIncoming = maplePacket.IsIncoming;
            _timestamp = maplePacket.Id;

            Opcode = BinaryPrimitives.ReadUInt16LittleEndian(_fullBuffer.AsSpan(4, 2));
        }

        public string Predict() => PacketUtils.Predict(Payload);

        public void Dispose()
        {
            if (_fullBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_fullBuffer);
                _fullBuffer = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
