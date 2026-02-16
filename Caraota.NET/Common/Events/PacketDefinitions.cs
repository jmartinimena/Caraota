using Caraota.NET.Core.Models.Views;

namespace Caraota.NET.Common.Events
{
    public delegate void PacketHandlerDelegate(ref MaplePacketView packet);
    public delegate void PacketReceivedDelegate(MaplePacketEventArgs args);
    public delegate void PacketDecryptedDelegate(MapleSessionViewEventArgs args);
    public delegate void HandshakeReceivedDelegate(HandshakePacketViewEventArgs args);
    public delegate void DivertPacketReceivedDelegate(WinDivertPacketViewEventArgs args);
}
