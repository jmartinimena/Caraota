namespace Caraota.Crypto.State
{
    public interface IMapleEncryptor
    {
        public byte[] SIV { get; }
        public byte[] RIV { get; }
        void Encrypt(Span<byte> payload, bool isIncoming, bool requiresContinuation = false);
    }
}
