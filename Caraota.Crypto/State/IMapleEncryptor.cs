using Caraota.Crypto.Packets;

namespace Caraota.Crypto.State
{
    public interface IMapleEncryptor
    {
        public byte[] SIV { get; }
        public byte[] RIV { get; }
        void Encrypt(ref MaplePacketView packet);
    }
}
