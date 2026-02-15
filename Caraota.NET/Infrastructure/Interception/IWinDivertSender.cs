using WinDivertSharp;

namespace Caraota.NET.Infrastructure.Interception
{
    public interface IWinDivertSender
    {
        void SendPacket(ReadOnlySpan<byte> packet, WinDivertAddress address);

        void ReplaceAndSend(ReadOnlySpan<byte> payload, Span<byte> destination, WinDivertAddress address);
    }
}
