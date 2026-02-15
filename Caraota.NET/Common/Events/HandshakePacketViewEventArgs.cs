using Caraota.Crypto.Utils;
using System.Buffers.Binary;

namespace Caraota.NET.Common.Events
{
    public readonly ref struct HandshakePacketViewEventArgs
    {
        private readonly int _sivOffset = 6;
        private readonly int _rivOffset = 10;
        private readonly int _localeOffset = 14;
        public MapleSessionViewEventArgs MapleSessionEventArgs { get; }
        public ushort Opcode { get; }
        public ReadOnlySpan<byte> Packet { get; }
        public ReadOnlySpan<byte> SIV { get; }
        public ReadOnlySpan<byte> RIV { get; }
        public ushort Version { get; }
        public ushort Subversion { get; }
        public byte Locale { get; }
        public long Timestamp { get; }
        public readonly string FormattedTime => PacketUtils.GetRealTime(Timestamp).ToString("HH:mm:ss:fff");

        public HandshakePacketViewEventArgs(MapleSessionViewEventArgs mapleSessionEventArgs, ReadOnlySpan<byte> packet)
        {
            Packet = packet;
            MapleSessionEventArgs = mapleSessionEventArgs;
            Timestamp = mapleSessionEventArgs.Address.Timestamp;

            var version = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(2, 2));
            if (version != 62)
            {
                _sivOffset++;
                _rivOffset++;
                _localeOffset++;
            }

            Version = BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]);
            Subversion = BinaryPrimitives.ReadUInt16LittleEndian(packet[4..]);
            SIV = Packet.Slice(_sivOffset, 4);
            RIV = Packet.Slice(_rivOffset, 4);
            Locale = packet[_localeOffset];
        }
    }
}
