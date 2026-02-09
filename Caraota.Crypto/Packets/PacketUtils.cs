using System.Diagnostics;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

using Caraota.Crypto.State;

namespace Caraota.Crypto.Packets
{
    public static class PacketUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static int GetLength(ReadOnlySpan<byte> header)
        {
            fixed (byte* ptr = header)
            {
                ushort versionMask = Unsafe.ReadUnaligned<ushort>(ptr);
                ushort lengthMask = Unsafe.ReadUnaligned<ushort>(ptr + 2);

                return versionMask ^ lengthMask;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> GetHeader(ReadOnlySpan<byte> iv, int length, bool isIncoming)
        {
            int a = (iv[3] << 8) | iv[2];

            a ^= isIncoming ? -(MapleCrypto.Version + 1) : MapleCrypto.Version;

            int b = a ^ length;

            Span<byte> header = new byte[4];

            BinaryPrimitives.WriteUInt16LittleEndian(header[..2], (ushort)a);
            BinaryPrimitives.WriteUInt16LittleEndian(header[2..], (ushort)b);

            return header;
        }

        public static string Predict(MaplePacketView packet)
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

        private static readonly DateTime _startTimeActual = DateTime.Now;
        private static readonly long _startTimeTimestamp = Stopwatch.GetTimestamp();

        public static DateTime GetRealTime(long packetTimestamp)
        {
            TimeSpan elapsedSinceStart = Stopwatch.GetElapsedTime(_startTimeTimestamp, packetTimestamp);
            return _startTimeActual + elapsedSinceStart;
        }
    }
}
