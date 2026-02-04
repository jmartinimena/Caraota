using WinDivertSharp;

namespace Caraota.NET.Interception
{
    public interface IWinDivertSender
    {
        void SendPacket(ReadOnlySpan<byte> packet, WinDivertAddress address, bool log = false);
    }
}
