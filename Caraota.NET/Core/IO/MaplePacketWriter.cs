using System.Text;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

using Caraota.Crypto.State;

namespace Caraota.NET.Core.IO
{
    // TODO: Debes deslizar los valores antes de la escritura
    public ref struct MaplePacketWriter : IDisposable
    {
        private int _position;
        private Span<byte> _payload;
        private readonly byte[] _payloadBuffer = ArrayPool<byte>.Shared.Rent(65536);
        public MaplePacketWriter(Span<byte> payload)
        {
            _payload = payload;
        }

        public MaplePacketWriter(MaplePacketView maplePacket)
        {
            _payload = maplePacket.Payload[2..];
        }

        public void WriteBoolean(bool value, int? position = null)
        {
            int pos = UpdatePosition<bool>(position);
            _payload[pos] = (byte)(value ? 1 : 0);
        }

        public void WriteUShort(ushort value, int? position = null)
        {
            int pos = UpdatePosition<ushort>(position);
            BinaryPrimitives.WriteUInt16LittleEndian(_payload[pos..], value);
        }

        public void WriteShort(short value, int? position = null)
        {
            int pos = UpdatePosition<short>(position);
            BinaryPrimitives.WriteInt16LittleEndian(_payload[pos..], value);
        }

        public void WriteUInt(uint value, int? position = null)
        {
            int pos = UpdatePosition<uint>(position);
            BinaryPrimitives.WriteUInt32LittleEndian(_payload[pos..], value);
        }

        public void WriteInt(int value, int? position = null)
        {
            int pos = UpdatePosition<int>(position);
            BinaryPrimitives.WriteInt32LittleEndian(_payload[pos..], value);
        }

        public void WriteULong(ulong value, int? position = null)
        {
            int pos = UpdatePosition<ulong>(position);
            BinaryPrimitives.WriteUInt64LittleEndian(_payload[pos..], value);
        }

        public void WriteLong(long value, int? position = null)
        {
            int pos = UpdatePosition<long>(position);
            BinaryPrimitives.WriteInt64LittleEndian(_payload[pos..], value);
        }

        public void WriteString(string value, int? position = null)
        {
            ushort len = (ushort)(value?.Length ?? 0);
            int pos = UpdatePosition<ushort>(position);

            WriteUShort(len, pos);

            if (len > 0)
            {
                int stringPos = pos + sizeof(ushort);
                UpdatePosition<ushort>(position, len);

                Encoding.ASCII.GetBytes(value, _payload.Slice(stringPos, len));
            }
        }

        public void WriteBytes(ReadOnlySpan<byte> bytes, int? position = null)
        {
            int pos = UpdatePosition<byte>(position, bytes.Length);
            bytes.CopyTo(_payload.Slice(pos, bytes.Length));
        }

        private int UpdatePosition<T>(int? position = null, int? len = null) where T : struct
        {
            int pos = position ?? _position;

            pos += len ?? Unsafe.SizeOf<T>();

            if (pos > _payload.Length)
            {
                _payload.CopyTo(_payloadBuffer);
                _payload = _payloadBuffer.AsSpan(0, _payload.Length + pos);
            }

            return pos;
        }

        public readonly void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_payloadBuffer);
        }
    }
}
