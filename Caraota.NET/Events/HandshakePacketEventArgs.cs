using System.Buffers.Binary;

namespace Caraota.NET.Events
{
    public readonly ref struct HandshakePacketEventArgs
    {
        public MapleSessionEventArgs MapleSessionEventArgs { get; }
        public ReadOnlySpan<byte> Packet { get; }
        public ReadOnlySpan<byte> Payload { get; }
        public readonly ushort Opcode { get; }
        public readonly ushort Version { get; }
        public ReadOnlySpan<byte> SIV { get; }
        public ReadOnlySpan<byte> RIV { get; }
        public readonly byte Locale { get; }
        public readonly DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        public HandshakePacketEventArgs(MapleSessionEventArgs mapleSessionEventArgs, ReadOnlySpan<byte> packet)
        {
            MapleSessionEventArgs = mapleSessionEventArgs;
            Packet = packet;
            Payload = packet[2..];
            Opcode = BinaryPrimitives.ReadUInt16LittleEndian(packet[..2]);
            Version = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(2, 2));

            if (Version == 62)
            {
                SIV = Packet.Slice(6, 4);
                RIV = Packet.Slice(10, 4);
                Locale = Packet[14];
            }
            else
            {
                SIV = Packet.Slice(7, 4);
                RIV = Packet.Slice(11, 4);
                Locale = Packet[15];
            }
        }
    }
}
