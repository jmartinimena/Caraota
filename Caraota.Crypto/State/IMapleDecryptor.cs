using Caraota.Crypto.Packets;

namespace Caraota.Crypto.State
{
    public interface IMapleDecryptor
    {
        public byte[] SIV { get; }
        public byte[] RIV { get; }
        void Decrypt(ref MaplePacketView packet);
    }
}
