using System.Diagnostics;
using System.Buffers.Binary;

namespace Caraota.Crypto.Packets
{
    public ref struct DecodedPacket
    {
        public readonly long Id { get; init; }
        public readonly bool IsIncoming { get; init; }
        public ReadOnlySpan<byte> IV { get; init; }
        public ReadOnlySpan<byte> Data { get; init; }
        public ReadOnlySpan<byte> Header { get; init; }
        public Span<byte> Payload { get; init; }
        public ReadOnlySpan<byte> Leftovers { get; init; }
        public int ParentReaded { get; set; }
        public readonly int TotalLength => Header.Length + Payload.Length + Leftovers.Length;

        public readonly ushort Opcode => BinaryPrimitives.ReadUInt16LittleEndian(Payload[..2]);

        public DecodedPacket(ReadOnlySpan<byte> data, ReadOnlySpan<byte> iv, bool isIncoming, long? parentId = null, int? parentReaded = null)
        {
            Id = parentId ?? Stopwatch.GetTimestamp();
            ParentReaded = parentReaded ?? 0;

            ReadOnlySpan<byte> headerSpan = data[..4];
            int payloadLength = PacketUtils.GetLength(headerSpan);
            headerSpan = PacketUtils.GetHeader(iv, payloadLength, isIncoming);

            if (payloadLength > data.Length - 4)
                payloadLength = data.Length - 4;

            Span<byte> buffer = new byte[data.Length];
            headerSpan.CopyTo(buffer[..4]);
            data[4..].CopyTo(buffer[4..]);

            Span<byte> bufferIv = new byte[iv.Length];
            iv.CopyTo(bufferIv);

            int totalProcessed = payloadLength + 4;

            IV = bufferIv;
            Header = headerSpan;
            IsIncoming = isIncoming;
            Payload = buffer.Slice(4, payloadLength);
            Data = buffer[..totalProcessed];
            Leftovers = buffer[totalProcessed..];
        }
    }
}
