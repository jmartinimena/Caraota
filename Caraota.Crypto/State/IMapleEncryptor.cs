using Caraota.Crypto.Packets;

namespace Caraota.Crypto.State
{
    public interface IMapleEncryptor
    {
        Memory<byte> IV { get; }
        void Encrypt(ref DecodedPacket packet);
        bool Validate(DecodedPacket packet);
    }
}
