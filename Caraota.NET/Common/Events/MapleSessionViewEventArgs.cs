using WinDivertSharp;

using Caraota.Crypto.State;
using Caraota.NET.Core.Models.Views;

namespace Caraota.NET.Common.Events
{
    public ref struct MapleSessionViewEventArgs(WinDivertPacketViewEventArgs args, MaplePacketView packet)
    {
        public bool Hijacked { get; set; }
        public readonly WinDivertAddress Address = args.Address;
        public readonly Span<byte> DivertPacketView = args.Packet;
        public MaplePacketView MaplePacketView = packet;
    }
}
