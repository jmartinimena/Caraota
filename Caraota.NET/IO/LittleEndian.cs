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

        public static void Write<T>(Span<byte> destination, T value, int pos = 0) where T : unmanaged
        {
            switch (value)
            {
                case bool b:
                    destination[pos] = (byte)(b ? 1 : 0);
                    break;

                case ushort us:
                    BinaryPrimitives.WriteUInt16LittleEndian(destination[pos..], us);
                    break;

                case short s:
                    BinaryPrimitives.WriteInt16LittleEndian(destination[pos..], s);
                    break;

                case uint ui:
                    BinaryPrimitives.WriteUInt32LittleEndian(destination[pos..], ui);
                    break;

                case int i:
                    BinaryPrimitives.WriteInt32LittleEndian(destination[pos..], i);
                    break;

                case ulong ul:
                    BinaryPrimitives.WriteUInt64LittleEndian(destination[pos..], ul);
                    break;

                case long l:
                    BinaryPrimitives.WriteInt64LittleEndian(destination[pos..], l);
                    break;

                default:
                    WriteStruct(destination, value, pos);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteStruct<T>(Span<byte> destination, T value, int pos) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();

            if (pos + size > destination.Length)
            {
                throw new InternalBufferOverflowException($"Buffer insuficiente para struct {typeof(T).Name}");
            }

            MemoryMarshal.Write(destination[pos..], in value);
        }

        public static Span<byte> WriteString(Span<byte> destination, string value, int pos = 0)
        {
            int len = value.Length;
            pos += 2;

            var beforeStringContent = destination[..pos];

            ushort currentLen = BinaryPrimitives.ReadUInt16LittleEndian(destination[pos..]);

            int afterStringPos = pos + sizeof(ushort) + currentLen;
            var afterStringContent = destination[afterStringPos..];

            int newTotalSize = beforeStringContent.Length + sizeof(ushort) + len + afterStringContent.Length;
            var newPayload = _payloadBuffer.AsSpan(0, newTotalSize);

            beforeStringContent.CopyTo(newPayload);

            BinaryPrimitives.WriteUInt16LittleEndian(newPayload[pos..], (ushort)len);

            if (len > 0)
            {
                int stringPos = pos + sizeof(ushort);
                Encoding.ASCII.GetBytes(value, newPayload.Slice(stringPos, len));
            }

            int newAfterPos = pos + sizeof(ushort) + len;
            afterStringContent.CopyTo(newPayload[newAfterPos..]);

            return newPayload;
        }

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
