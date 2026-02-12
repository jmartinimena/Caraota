using Caraota.NET.Protocol.Structures;
using Caraota.NET.Infrastructure.Interception;

namespace Caraota.NET.Common.Events
{
    public readonly struct MaplePacketEventArgs(MapleSessionViewEventArgs args)
    {
        public readonly bool Hijacked  = args.Hijacked;
        public readonly MaplePacket Packet = new(args.MaplePacketView);
    }
}
