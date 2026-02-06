using WinDivertSharp;

namespace Caraota.NET.Common.Events
{
    public readonly ref struct WinDivertPacketEventArgs(ReadOnlySpan<byte> packet, WinDivertAddress address, bool isIncoming)
    {
        public readonly bool IsIncoming = isIncoming;
        public readonly ReadOnlySpan<byte> Packet = packet;
        public readonly WinDivertAddress Address = address;
    }
}
