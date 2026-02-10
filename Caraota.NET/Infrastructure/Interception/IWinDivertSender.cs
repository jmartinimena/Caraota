using WinDivertSharp;

namespace Caraota.NET.Infrastructure.Interception
{
    public interface IWinDivertSender
    {
        void SendPacket(ReadOnlySpan<byte> packet, WinDivertAddress address);

        void ReplaceAndSend(Span<byte> original, ReadOnlySpan<byte> payload, WinDivertAddress address);
    }
}
