using System.Diagnostics;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

using Caraota.Crypto.State;

namespace Caraota.NET.Protocol.Structures
{
    public static class PacketUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static int GetLength(ReadOnlySpan<byte> header)
        {
            ushort versionMask = BinaryPrimitives.ReadUInt16LittleEndian(header);
            ushort lengthMask = BinaryPrimitives.ReadUInt16LittleEndian(header[2..]);
            return versionMask ^ lengthMask;
        }

        private static byte[] _headerBuffer = new byte[4];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ReadOnlySpan<byte> GetHeader(ReadOnlySpan<byte> iv, int length, bool isIncoming, ushort version)
        {
            int a = (iv[3] << 8) | iv[2];

            a ^= isIncoming ? -(version + 1) : version;

            int b = a ^ length;

            fixed (byte* headerPtr = _headerBuffer)
            {
                Unsafe.WriteUnaligned(headerPtr, (ushort)a);
                Unsafe.WriteUnaligned(headerPtr + 2, (ushort)b);
            }

            return _headerBuffer;
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
