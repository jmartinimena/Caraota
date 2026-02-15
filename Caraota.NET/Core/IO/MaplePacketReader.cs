using System.Text;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

using Caraota.NET.Core.Models;

namespace Caraota.NET.Core.IO
{
    public ref struct MaplePacketReader
    {
        private int _readerPos;
        private readonly ReadOnlySpan<byte> _payload;
        public MaplePacketReader(ReadOnlySpan<byte> payload)
        {
            _payload = payload;
        }

        public MaplePacketReader(MaplePacket maplePacket)
        {
            _payload = maplePacket.Payload.Span[2..];
        }

        public bool ReadBoolean(int? position = null)
        {
            int pos = UpdatePosition<bool>(position);
            byte raw = _payload[pos];
            return raw != 0;
        }

        public ushort ReadUShort(int? position = null)
        {
            int pos = UpdatePosition<ushort>(position);
            return BinaryPrimitives.ReadUInt16LittleEndian(_payload[pos..]);
        }

        public short ReadShort(int? position = null)
        {
            int pos = UpdatePosition<short>(position);
            return BinaryPrimitives.ReadInt16LittleEndian(_payload[pos..]);
        }

        public uint ReadUInt(int? position = null)
        {
            int pos = UpdatePosition<uint>(position);
            return BinaryPrimitives.ReadUInt32LittleEndian(_payload[pos..]);
        }

        public int ReadInt(int? position = null)
        {
            int pos = UpdatePosition<int>(position);
            return BinaryPrimitives.ReadInt32LittleEndian(_payload[pos..]);
        }

        public ulong ReadULong(int? position = null)
        {
            int pos = UpdatePosition<ulong>(position);
            return BinaryPrimitives.ReadUInt64LittleEndian(_payload[pos..]);
        }

        public long ReadLong(int? position = null)
        {
            int pos = UpdatePosition<long>(position);
            return BinaryPrimitives.ReadInt64LittleEndian(_payload[pos..]);
        }

        public string ReadString(int? position = null)
        {
            int pos = UpdatePosition<ushort>(position);
            ushort len = ReadUShort(pos);
            UpdatePosition<ushort>(pos, len);
            int stringPos = pos + sizeof(ushort);
            return Encoding.ASCII.GetString(_payload.Slice(stringPos, len));
        }

        private int UpdatePosition<T>(int? position = 0, int? len = 0) where T : struct 
        {
            int pos = position ?? _readerPos;

            if (position is null)
                _readerPos += len ?? Unsafe.SizeOf<T>();

            return pos;
        }
    }
}
