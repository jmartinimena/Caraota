using Microsoft.Extensions.ObjectPool;

using Caraota.Crypto.Packets;

namespace Caraota.NET.Performance
{
    public static class Pools
    {
        public static readonly ObjectPool<MaplePacket> MaplePackets = new DefaultObjectPool<MaplePacket>(new DefaultPooledObjectPolicy<MaplePacket>());
    }
}
