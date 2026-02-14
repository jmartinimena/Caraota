using WinDivertSharp;

using Caraota.Crypto.State;
using Caraota.NET.Common.Events;

namespace Caraota.NET.Infrastructure.Interception
{
    public ref struct MapleSessionViewEventArgs(WinDivertPacketViewEventArgs args, MaplePacketView packet)
    {
        public bool Hijacked { get; set; }
        public readonly WinDivertAddress Address = args.Address;
        public readonly Span<byte> DivertPacketView = args.Packet;
        public MaplePacketView MaplePacketView = packet;
    }
}
