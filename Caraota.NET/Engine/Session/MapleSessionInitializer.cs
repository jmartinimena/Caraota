using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Caraota.Crypto.State;
using Caraota.NET.Common.Events;
using Caraota.NET.Infrastructure.Interception;

namespace Caraota.NET.Engine.Session
{
    public class MapleSessionInitializer(IWinDivertSender winDivertSender)
    {
        public bool SessionSuccess { get; set; }

        private const int MAX_VERSION = 256;
        private const int HANDSHAKE_V82_LENGTH = 16;
        private const int HANDSHAKE_V62_LENGTH = 15;
        private const int VERSION_OFFSET = 2;
        private const int VERSION_SIZE = sizeof(ushort);

        private MapleCrypto? _serverRecv;
        private MapleCrypto? _serverSend;
        private MapleCrypto? _clientRecv;
        private MapleCrypto? _clientSend;

        private readonly IWinDivertSender _winDivertSender = winDivertSender;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMapleDecryptor? GetDecryptor(bool isIncoming)
        => isIncoming ? _serverRecv : _clientRecv;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMapleEncryptor? GetEncryptor(bool isIncoming)
        => isIncoming ? _serverSend : _clientSend;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HandshakeSessionPacket Initialize(WinDivertPacketEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        {
            if (TryGetVersion(payload, out ushort version))
            {
                return CreateCryptoInstances(winDivertPacket, payload, version);
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        private HandshakeSessionPacket CreateCryptoInstances(WinDivertPacketEventArgs args, ReadOnlySpan<byte> payload, ushort version)
        {
            var mapleSession = new MapleSessionPacket(args, default);
            var handshakePacket = new HandshakeSessionPacket(mapleSession, payload);

            _serverRecv = new MapleCrypto(handshakePacket.RIV, version);
            _serverSend = new MapleCrypto(handshakePacket.RIV, version);
            _clientRecv = new MapleCrypto(handshakePacket.SIV, version);
            _clientSend = new MapleCrypto(handshakePacket.SIV, version);

            SessionSuccess = true;

            _winDivertSender.ReplaceAndSend(args.Packet, payload, args.Address, isIncoming: true);

            return handshakePacket;
        }
    }
}
