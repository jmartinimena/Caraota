using System.Text;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Caraota.NET.IO
{
    public static class LittleEndianReader
    {
        public static T Read<T>(ReadOnlySpan<byte> source, int pos = 0) where T : unmanaged
        {
            return typeof(T) switch
            {
                var t when t == typeof(bool) => (T)(object)source[pos],
                var t when t == typeof(ushort) => (T)(object)BinaryPrimitives.ReadUInt16LittleEndian(source[pos..]),
                var t when t == typeof(short) => (T)(object)BinaryPrimitives.ReadInt16LittleEndian(source[pos..]),
                var t when t == typeof(uint) => (T)(object)BinaryPrimitives.ReadUInt32LittleEndian(source[pos..]),
                var t when t == typeof(int) => (T)(object)BinaryPrimitives.ReadInt32LittleEndian(source[pos..]),
                var t when t == typeof(ulong) => (T)(object)BinaryPrimitives.ReadUInt64LittleEndian(source[pos..]),
                var t when t == typeof(long) => (T)(object)BinaryPrimitives.ReadInt64LittleEndian(source[pos..]),

                _ => ReadStruct<T>(source)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ReadStruct<T>(ReadOnlySpan<byte> source) where T : unmanaged
        {
            if (source.Length < Unsafe.SizeOf<T>())
            {
                throw new ArgumentOutOfRangeException($"Buffer demasiado pequeño para struct {typeof(T).Name}");
            }

            return MemoryMarshal.Read<T>(source);
        }

        public static string ReadString(ReadOnlySpan<byte> source, int pos = 0)
        {
            ushort len = Read<ushort>(source, pos);
            int stringPos = pos + sizeof(ushort);
            return Encoding.ASCII.GetString(source.Slice(stringPos, len));
        }
    }
}
