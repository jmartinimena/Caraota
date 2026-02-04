using WinDivertSharp;

namespace Caraota.NET.Events
{
    public readonly ref struct WinDivertPacketEventArgs(ReadOnlySpan<byte> packet, WinDivertAddress address)
    {
        public readonly ReadOnlySpan<byte> Packet = packet;
        public readonly WinDivertAddress Address = address;
    }
}
