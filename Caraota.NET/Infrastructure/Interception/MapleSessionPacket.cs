using WinDivertSharp;

using Caraota.Crypto.Packets;
using Caraota.NET.Common.Events;

namespace Caraota.NET.Infrastructure.Interception
{
    public ref struct MapleSessionPacket(WinDivertPacketEventArgs args, DecodedPacket packet)
    {
        public bool Hijacked { get; set; }
        public readonly WinDivertAddress Address = args.Address;
        public readonly ReadOnlySpan<byte> WinDivertPacket = args.Packet;
        public DecodedPacket DecodedPacket = packet;
    }
}
