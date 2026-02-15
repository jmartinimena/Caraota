namespace Caraota.Crypto.State
{
    public interface IMapleDecryptor
    {
        public byte[] SIV { get; }
        public byte[] RIV { get; }
        void Decrypt(Span<byte> payload, bool isIncoming, bool requiresContinuation = false);
    }
}
