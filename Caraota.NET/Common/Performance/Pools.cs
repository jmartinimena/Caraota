using Microsoft.Extensions.ObjectPool;

using Caraota.Crypto.Packets;

namespace Caraota.NET.Common.Performance
{
    public static class Pools
    {
        public static readonly ObjectPool<MaplePacket> MaplePackets = new DefaultObjectPool<MaplePacket>(new DefaultPooledObjectPolicy<MaplePacket>());
    }
}
