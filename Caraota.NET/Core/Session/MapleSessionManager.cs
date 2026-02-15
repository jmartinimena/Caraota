using System.Buffers.Binary;

using Caraota.Crypto.State;
using Caraota.NET.Common.Events;
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

        public bool Initialize(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload, out HandshakePacketViewEventArgs packetView)
        {
            packetView = default;

            if (TryGetVersion(payload, out ushort version))
            {
                packetView = CreateCryptoInstances(winDivertPacket, payload, version);

                return true;
            }

            return false;
        }

        private static bool TryGetVersion(ReadOnlySpan<byte> data, out ushort version)
        {
            version = 0;

            switch (data.Length)
            {
                case HANDSHAKE_V82_LENGTH:
                case HANDSHAKE_V62_LENGTH:
                    if (data.Length >= VERSION_OFFSET + VERSION_SIZE)
                    {
                        version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(VERSION_OFFSET, VERSION_SIZE));
                        return version <= MAX_VERSION;
                    }
                    break;
            }

            return false;
        }

        private HandshakePacketViewEventArgs CreateCryptoInstances(WinDivertPacketViewEventArgs args, ReadOnlySpan<byte> payload, ushort version)
        {
            var mapleSession = new MapleSessionViewEventArgs(args, default);
            var handshakePacket = new HandshakePacketViewEventArgs(mapleSession, payload);

            Encryptor = new MapleCrypto(handshakePacket.SIV, handshakePacket.RIV, version);
            Decryptor = new MapleCrypto(handshakePacket.SIV, handshakePacket.RIV, version);

            Success = true;

            _winDivertSender.ReplaceAndSend(args.Packet, payload, args.Address);

            return handshakePacket;
        }
    }
}
