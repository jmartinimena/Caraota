using Caraota.Crypto.Packets;

namespace Caraota.NET.Events
{
    public class MaplePacketEventArgs(MaplePacket packet, bool hijacked)
    {
        public bool Hijacked { get; private set; } = hijacked;
        public MaplePacket Packet { get; set; } = packet;
    }
}
