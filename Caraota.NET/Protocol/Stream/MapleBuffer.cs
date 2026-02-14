using System.Buffers;

namespace Caraota.NET.Protocol.Stream
{
    public readonly struct MapleBuffer(int length) : IDisposable
    {
        public byte[] Buffer { get; } = ArrayPool<byte>.Shared.Rent(length);
        public int Length { get; } = length;

        public Span<byte> AsSpan() => Buffer.AsSpan(0, Length);
        public Span<byte> AsSpan(int start) => Buffer.AsSpan(start);
        public Span<byte> AsSpan(int start, int length) => Buffer.AsSpan(start, length);

        public void CopyTo(MapleBuffer destination) => AsSpan().CopyTo(destination.AsSpan());

        public void Dispose()
        {
            if (Buffer != null)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
    }
}
