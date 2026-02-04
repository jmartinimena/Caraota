using WinDivertSharp;

using Caraota.Crypto.Packets;

namespace Caraota.NET.Events
{
    public ref struct MapleSessionEventArgs(WinDivertPacketEventArgs args, DecodedPacket packet)
    {
        public bool Hijacked { get; set; }
        public WinDivertAddress Address { get; private set; } = args.Address;
        public ReadOnlySpan<byte> WinDivertPacket { get; private set; } = args.Packet;
        public DecodedPacket DecodedPacket { get; set;  } = packet;
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    }
}
