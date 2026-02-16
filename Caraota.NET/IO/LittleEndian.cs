using System.Text;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Caraota.NET.IO
{
    public static class LittleEndian
    {
        private static readonly byte[] _payloadBuffer = ArrayPool<byte>.Shared.Rent(65536);

        public static void Write<T>(Span<byte> destination, T value, int start = 0) where T : unmanaged
        {
            switch (value)
            {
                case byte b:
                    destination[start] = b;
                    break;
                case bool b:
                    destination[start] = (byte)(b ? 1 : 0);
                    break;

                case ushort us:
                    BinaryPrimitives.WriteUInt16LittleEndian(destination[start..], us);
                    break;

                case short s:
                    BinaryPrimitives.WriteInt16LittleEndian(destination[start..], s);
                    break;

                case uint ui:
                    BinaryPrimitives.WriteUInt32LittleEndian(destination[start..], ui);
                    break;

                case int i:
                    BinaryPrimitives.WriteInt32LittleEndian(destination[start..], i);
                    break;

                case ulong ul:
                    BinaryPrimitives.WriteUInt64LittleEndian(destination[start..], ul);
                    break;

                case long l:
                    BinaryPrimitives.WriteInt64LittleEndian(destination[start..], l);
                    break;

                default:
                    WriteStruct(destination, value, start);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteStruct<T>(Span<byte> destination, T value, int start) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();

            if (start + size > destination.Length)
            {
                throw new InternalBufferOverflowException($"Buffer insuficiente para struct {typeof(T).Name}");
            }

            MemoryMarshal.Write(destination[start..], in value);
        }

        public static Span<byte> WriteString(Span<byte> destination, string value, int start = 0)
        {
            int len = value.Length;

            var beforeStringContent = destination[..start];

            ushort currentLen = BinaryPrimitives.ReadUInt16LittleEndian(destination[start..]);

            int afterStringPos = start + sizeof(ushort) + currentLen;
            var afterStringContent = destination[afterStringPos..];

            int newTotalSize = beforeStringContent.Length + sizeof(ushort) + len + afterStringContent.Length;
            var newPayload = _payloadBuffer.AsSpan(0, newTotalSize);

            beforeStringContent.CopyTo(newPayload);

            BinaryPrimitives.WriteUInt16LittleEndian(newPayload[start..], (ushort)len);

            if (len > 0)
            {
                int stringPos = start + sizeof(ushort);
                Encoding.ASCII.GetBytes(value, newPayload.Slice(stringPos, len));
            }

            int newAfterPos = start + sizeof(ushort) + len;
            afterStringContent.CopyTo(newPayload[newAfterPos..]);

            return newPayload;
        }

        public static T Read<T>(ReadOnlySpan<byte> source, int start = 0) where T : unmanaged
        {
            return typeof(T) switch
            {
                var t when t == typeof(byte) => (T)(object)source[start],
                var t when t == typeof(bool) => (T)(object)source[start],
                var t when t == typeof(ushort) => (T)(object)BinaryPrimitives.ReadUInt16LittleEndian(source[start..]),
                var t when t == typeof(short) => (T)(object)BinaryPrimitives.ReadInt16LittleEndian(source[start..]),
                var t when t == typeof(uint) => (T)(object)BinaryPrimitives.ReadUInt32LittleEndian(source[start..]),
                var t when t == typeof(int) => (T)(object)BinaryPrimitives.ReadInt32LittleEndian(source[start..]),
                var t when t == typeof(ulong) => (T)(object)BinaryPrimitives.ReadUInt64LittleEndian(source[start..]),
                var t when t == typeof(long) => (T)(object)BinaryPrimitives.ReadInt64LittleEndian(source[start..]),

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

        public static string ReadString(ReadOnlySpan<byte> source, int start = 0)
        {
            ushort len = Read<ushort>(source, start);
            int stringPos = start + sizeof(ushort);
            return Encoding.ASCII.GetString(source.Slice(stringPos, len));
        }

        public static ReadOnlySpan<byte> ReadBytes(ReadOnlySpan<byte> source, int start, int len)
        {
            return source.Slice(start, len);
        }
    }
}
