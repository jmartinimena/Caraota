using Caraota.Crypto.Packets;

namespace Caraota.Crypto.State
{
    public interface IMapleEncryptor
    {
        Memory<byte> IV { get; }
        void Encrypt(ref MaplePacketView packet);
        bool Validate(MaplePacketView packet);
    }
}
