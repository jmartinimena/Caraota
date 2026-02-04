using Caraota.Crypto.Packets;

namespace Caraota.Crypto.Processing
{
    public static class PacketFactory
    {
        public static MaplePacket? Create(
            ReadOnlySpan<byte> data,
            ReadOnlySpan<byte> iv,
            bool isIncoming)
        {
            if (data.Length < 4) return null;

            var decodedPacket = new DecodedPacket(data, iv, isIncoming);
            return new MaplePacket(decodedPacket);
        }

        public static DecodedPacket Parse(
            ReadOnlySpan<byte> data,
            ReadOnlySpan<byte> iv,
            bool isIncoming,
            long? parentId = null,
            int? parentReaded = null)
        {
            if (data.Length < 4) return default!;

            return new DecodedPacket(data, iv, isIncoming, parentId, parentReaded);
        }

        public static DecodedPacket Parse(MaplePacket packet)
        {
            if (packet.DataLen < 4) return default!;

            return new DecodedPacket(
                packet.Data.AsSpan(0, packet.DataLen), 
                packet.IV.AsSpan(0, packet.IvLen), 
                packet.IsIncoming);
        }
    }
}
