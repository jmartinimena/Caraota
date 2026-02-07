using Caraota.NET.Engine.Session;

using Caraota.Crypto.State;
using Caraota.Crypto.Packets;

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

            encryptor.Encrypt(ref packet);
        }

        public void Decrypt(ref DecodedPacket packet)
        {
            var decryptor = packet.IsIncoming ? _serverDecryptor : _clientDecryptor;

            decryptor.Decrypt(ref packet);
        }
    }
}
