using WinDivertSharp;

namespace Caraota.NET.Common.Events
{
    public readonly ref struct WinDivertPacketViewEventArgs(Span<byte> packet, WinDivertAddress address, bool isIncoming)
    {
        public readonly bool IsIncoming = isIncoming;
        public readonly Span<byte> Packet = packet;
        public readonly WinDivertAddress Address = address;
    }
}
