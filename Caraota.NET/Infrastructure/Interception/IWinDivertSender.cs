using WinDivertSharp;

namespace Caraota.NET.Infrastructure.Interception
{
    public interface IWinDivertSender
    {
        void SendPacket(ReadOnlySpan<byte> packet, WinDivertAddress address, bool log = false);

        void ReplaceAndSend(ReadOnlySpan<byte> original, ReadOnlySpan<byte> payload, WinDivertAddress address);
    }
}
