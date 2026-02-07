using Caraota.Crypto.Packets;
using Caraota.Crypto.State;
using Caraota.NET.Engine.Session;

namespace Caraota.NET.Engine.Logic
{
    public class MaplePacketProcessor(MapleSessionInitializer sessionInitializer)
    {
        private readonly IMapleDecryptor _serverDecryptor = sessionInitializer.GetDecryptor(true)!;
        private readonly IMapleDecryptor _clientDecryptor = sessionInitializer.GetDecryptor(false)!;

        private readonly IMapleEncryptor _serverEncryptor = sessionInitializer.GetEncryptor(true)!;
        private readonly IMapleEncryptor _clientEncryptor = sessionInitializer.GetEncryptor(false)!;

        public void Encrypt(ref DecodedPacket packet)
        {
            var encryptor = packet.IsIncoming ? _serverEncryptor : _clientEncryptor;

            if (encryptor.Validate(packet))
                encryptor.Encrypt(ref packet);
        }

        public void Decrypt(ref DecodedPacket packet)
        {
            var decryptor = packet.IsIncoming ? _serverDecryptor : _clientDecryptor;

            if (decryptor.Validate(packet))
                decryptor.Decrypt(ref packet);
        }

        public bool ValidateDecrypt(DecodedPacket packet)
        {
            var decryptor = packet.IsIncoming ? _serverDecryptor : _clientDecryptor;

            return decryptor.Validate(packet);
        }

        public bool ValidateEncrypt(DecodedPacket packet)
        {
            var decryptor = packet.IsIncoming ? _serverEncryptor : _clientEncryptor;

            return decryptor.Validate(packet);
        }
    }
}
