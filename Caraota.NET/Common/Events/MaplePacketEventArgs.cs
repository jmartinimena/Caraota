using Caraota.Crypto.Packets;

namespace Caraota.NET.Common.Events
{
    public readonly struct MaplePacketEventArgs(MaplePacket packet, bool hijacked)
    {
        public readonly bool Hijacked  = hijacked;
        public readonly MaplePacket Packet = packet;
    }
}
