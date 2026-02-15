using System.Buffers;
using System.Buffers.Binary;

namespace Caraota.NET.Infrastructure.TCP
{
    public sealed class TcpStackArchitect : IDisposable
    {
        private ushort _fakeSeq = 0, _fakeAck = 0;
        private readonly byte[] _tcpBuffer = ArrayPool<byte>.Shared.Rent(65536);

        public unsafe void ReplacePayload(ReadOnlySpan<byte> payload, Span<byte> destination, bool isIncoming)
        {
            fixed (byte* pPacket = destination)
            {
                int ipH = (*pPacket & 0x0F) << 2;

                int tcpH = ((*(pPacket + ipH + 12) >> 4) & 0x0F) << 2;

                int totalHeader = ipH + tcpH;
                int totalSize = totalHeader + payload.Length;

                if(totalSize > destination.Length)
                {
                    destination[..totalHeader].CopyTo(_tcpBuffer);
                    destination = _tcpBuffer.AsSpan(0, totalSize);
                }

                payload.CopyTo(destination[totalHeader..]);

                uint* pSeqPtr = (uint*)(pPacket + ipH + 4);
                uint* pAckPtr = (uint*)(pPacket + ipH + 8);

                uint currentSeq = BinaryPrimitives.ReverseEndianness(*pSeqPtr);
                uint currentAck = BinaryPrimitives.ReverseEndianness(*pAckPtr);

                ushort delta = (ushort)Math.Abs(destination.Length - totalSize);
                uint finalSeq, finalAck;

                if (isIncoming)
                {
                    finalSeq = currentSeq + _fakeAck;
                    finalAck = currentAck + _fakeSeq;
                    _fakeAck += delta;
                }
                else
                {
                    finalSeq = currentSeq + _fakeSeq;
                    finalAck = currentAck + _fakeAck;
                    _fakeSeq += delta;
                }

                *pSeqPtr = BinaryPrimitives.ReverseEndianness(finalSeq);
                *pAckPtr = BinaryPrimitives.ReverseEndianness(finalAck);

                *(ushort*)(pPacket + 2) = BinaryPrimitives.ReverseEndianness((ushort)totalSize);
            }
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_tcpBuffer);
            GC.SuppressFinalize(this);
        }
    }
}
