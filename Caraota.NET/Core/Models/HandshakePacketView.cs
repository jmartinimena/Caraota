using System.Buffers.Binary;
using Caraota.NET.Common.Events;

namespace Caraota.NET.Core.Models
{
    public readonly ref struct HandshakePacketView
    {
        public readonly int SIVOffset = 6;
        public readonly int RIVOffset = 10;
        public readonly int LocaleOffset = 14;
        public MapleSessionViewEventArgs MapleSessionEventArgs { get; }
        public ReadOnlySpan<byte> Packet { get; }
        public ReadOnlySpan<byte> SIV { get; }
        public ReadOnlySpan<byte> RIV { get; }
        public long Timestamp { get; }

        public HandshakePacketView(MapleSessionViewEventArgs mapleSessionEventArgs, ReadOnlySpan<byte> packet)
        {
            Packet = packet;
            MapleSessionEventArgs = mapleSessionEventArgs;
            Timestamp = mapleSessionEventArgs.Address.Timestamp;

            var version = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(2, 2));
            if (version != 62)
            {
                SIVOffset++;
                RIVOffset++;
                LocaleOffset++;
            }

            SIV = Packet.Slice(SIVOffset, 4);
            RIV = Packet.Slice(RIVOffset, 4);
        }
    }
}
