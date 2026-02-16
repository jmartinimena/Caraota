using Caraota.Crypto.State;

using Caraota.NET.Common.Events;

using Caraota.NET.Core.Models.Views;

using Caraota.NET.Infrastructure.Interception;

namespace Caraota.NET.Core.Session
{
    public class MapleSessionManager(IWinDivertSender winDivertSender)
    {
        public bool Success { get; set; }

        private const int MAX_VERSION = 256;
        private const int HANDSHAKE_V82_LENGTH = 16;
        private const int HANDSHAKE_V62_LENGTH = 15;
        private const int VERSION_OFFSET = 2;
        private const int VERSION_SIZE = sizeof(ushort);

        public IMapleDecryptor Decryptor = default!;
        public IMapleEncryptor Encryptor = default!;

        private readonly IWinDivertSender _winDivertSender = winDivertSender;

        public bool Initialize(WinDivertPacketViewEventArgs winDivertPacket, MaplePacketView packet, out HandshakePacketViewEventArgs packetView)
        {
            packet.SetAsRaw();

            packetView = default;

            if (IsHandshakePacket(packet) && TryGetVersion(packet, out ushort version))
            {
                packetView = CreateCryptoInstances(winDivertPacket, packet, version);

                return true;
            }

            return false;
        }

        private static bool IsHandshakePacket(MaplePacketView packet)
        => packet.Opcode is 13 or 14;

        private static bool TryGetVersion(MaplePacketView packet, out ushort version)
        {
            version = 0;

            switch (packet.Data.Length)
            {
                case HANDSHAKE_V82_LENGTH:
                case HANDSHAKE_V62_LENGTH:
                    if (packet.Data.Length >= VERSION_OFFSET + VERSION_SIZE)
                    {
                        version = packet.Read<ushort>(2);
                        return version <= MAX_VERSION;
                    }
                    break;
            }

            return false;
        }

        private HandshakePacketViewEventArgs CreateCryptoInstances(WinDivertPacketViewEventArgs args, MaplePacketView packet, ushort version)
        {
            var mapleSession = new MapleSessionViewEventArgs(args, packet);
            var handshakePacket = new HandshakePacketViewEventArgs(mapleSession);

            Encryptor = new MapleCrypto(handshakePacket.SIV, handshakePacket.RIV, version);
            Decryptor = new MapleCrypto(handshakePacket.SIV, handshakePacket.RIV, version);

            Success = true;

            _winDivertSender.SendPacket(args.Packet, args.Address);

            return handshakePacket;
        }
    }
}
