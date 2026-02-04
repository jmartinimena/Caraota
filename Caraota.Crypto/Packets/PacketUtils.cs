using System.Buffers.Binary;
using System.Runtime.CompilerServices;

using Caraota.Crypto.Processing;

namespace Caraota.Crypto.Packets
{
    public static class PacketUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLength(ReadOnlySpan<byte> header)
        {
            if (header.Length < 4)
                return -1;

            ushort versionMask = BinaryPrimitives.ReadUInt16LittleEndian(header[..2]);
            ushort lengthMask = BinaryPrimitives.ReadUInt16LittleEndian(header[2..4]);

            return versionMask ^ lengthMask;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort GetOpcode(ReadOnlySpan<byte> payload)
        {
            return payload.Length >= 2
                ? BinaryPrimitives.ReadUInt16LittleEndian(payload[..2])
                : (ushort)0;
        }

        private static readonly byte[] _headerBuffer = new byte[4];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> GetHeader(ReadOnlySpan<byte> iv, int length, bool isIncoming)
        {
            int a = (iv[3] << 8) | iv[2];

            a ^= isIncoming ? -(MapleCrypto.Version + 1) : MapleCrypto.Version;

            int b = a ^ length;

            Span<byte> header = _headerBuffer;

            BinaryPrimitives.WriteUInt16LittleEndian(header[..2], (ushort)a);
            BinaryPrimitives.WriteUInt16LittleEndian(header[2..], (ushort)b);

            return header;
        }

        public static string Predict(DecodedPacket packet)
        {
            if (packet.Payload.Length <= 4)
                return string.Empty;

            var structurePredictor = new StructurePredictor(packet);
            return structurePredictor.GetStructure();
        }

        public static string Predict(byte[] packet)
        {
            if (packet.Length <= 4)
                return string.Empty;

            var structurePredictor = new StructurePredictor(packet);
            return structurePredictor.GetStructure();
        }

        public static string Predict(ReadOnlyMemory<byte> packet)
        {
            if (packet.Length <= 4)
                return string.Empty;

            var structurePredictor = new StructurePredictor(packet);
            return structurePredictor.GetStructure();
        }
    }
}
