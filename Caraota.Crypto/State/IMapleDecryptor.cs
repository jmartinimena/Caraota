using Caraota.Crypto.Packets;

namespace Caraota.Crypto.State
{
    public interface IMapleDecryptor
    {
        Memory<byte> IV { get; }
        void Decrypt(ref DecodedPacket packet);
        bool Validate(DecodedPacket packet);
    }
}
