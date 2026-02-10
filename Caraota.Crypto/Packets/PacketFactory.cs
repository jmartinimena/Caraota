namespace Caraota.Crypto.Packets
{
    public static class PacketFactory
    {
        public static MaplePacket? Create(
            Span<byte> data,
            ReadOnlySpan<byte> iv,
            bool isIncoming)
        {
            if (data.Length < 4) return null;

            var decodedPacket = new MaplePacketView(data, iv, isIncoming);
            return new MaplePacket(decodedPacket);
        }

        public static MaplePacketView Parse(
            Span<byte> data,
            ReadOnlySpan<byte> iv,
            bool isIncoming,
            long? parentId = null,
            int? parentReaded = null)
        {
            return new MaplePacketView(data, iv, isIncoming, parentId, parentReaded);
        }

        public static MaplePacketView Parse(MaplePacket packet)
        {
            return new MaplePacketView(
                packet.Data.Span, 
                packet.IV.Span, 
                packet.IsIncoming);
        }
    }
}
