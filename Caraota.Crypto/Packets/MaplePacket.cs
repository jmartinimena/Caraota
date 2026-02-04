using System.Buffers;
using System.Buffers.Binary;

namespace Caraota.Crypto.Packets
{
    public class MaplePacket : IDisposable
    {
        public bool IsIncoming { get; private set; }
        public int DataLen { get; private set; }
        public byte[] Data { get; private set; }
        public int IvLen { get; private set; }
        public byte[] IV { get; private set; }
        public int HeaderLen { get; private set; }
        public byte[] Header { get; private set; }
        public int PayloadLen { get; private set; }
        public byte[] Payload { get; private set; }
        public ushort Opcode { get; private set; }
        public readonly DateTimeOffset Timestamp = DateTimeOffset.UtcNow;
        public string IVStr => Convert.ToHexString(IV, 0, IvLen);
        public string HeaderStr => Convert.ToHexString(Header, 0, HeaderLen);
        public string PayloadStr => Convert.ToHexString(Payload, 0, PayloadLen);
        public string ToHexString() => Convert.ToHexString(Data, 0, DataLen);
        public MaplePacket(DecodedPacket maplePacket)
        {
            DataLen = maplePacket.Data.Length;
            IvLen = maplePacket.IV.Length;
            HeaderLen = maplePacket.Header.Length;
            PayloadLen = maplePacket.Payload.Length;

            Data = ArrayPool<byte>.Shared.Rent(maplePacket.Data.Length);
            maplePacket.Data.CopyTo(Data);

            IV = ArrayPool<byte>.Shared.Rent(maplePacket.IV.Length);
            maplePacket.IV.CopyTo(IV);

            Header = ArrayPool<byte>.Shared.Rent(maplePacket.Header.Length);
            maplePacket.Header.CopyTo(Header);

            Payload = ArrayPool<byte>.Shared.Rent(maplePacket.Payload.Length);
            maplePacket.Payload.CopyTo(Payload);

            Opcode = BinaryPrimitives.ReadUInt16LittleEndian(Payload.AsSpan(0, 2));

            IsIncoming = maplePacket.IsIncoming;
        }

        public string Predict() => PacketUtils.Predict(Payload.AsMemory(0, PayloadLen));

        public void Dispose()
        {
            if (Data != null) ArrayPool<byte>.Shared.Return(Data);
            if (IV != null) ArrayPool<byte>.Shared.Return(IV);
            if (Header != null) ArrayPool<byte>.Shared.Return(Header);
            if (Payload != null) ArrayPool<byte>.Shared.Return(Payload);

            Data = null!;
            IV = null!;
            Header = null!;
            Payload = null!;

            GC.SuppressFinalize(this);
        }
    }
}
