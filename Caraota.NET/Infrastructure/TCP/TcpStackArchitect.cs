using System.Buffers.Binary;

namespace Caraota.NET.Infrastructure.TCP
{
    public sealed class TcpStackArchitect
    {
        private ushort _fakeSeq = 0, _fakeAck = 0;
        public unsafe void ReplacePayload(Span<byte> tcpPacket, ReadOnlySpan<byte> payload, bool isIncoming)
        {
            fixed (byte* pPacket = tcpPacket)
            {
                int ipH = (*pPacket & 0x0F) << 2;

                int tcpH = ((*(pPacket + ipH + 12) >> 4) & 0x0F) << 2;

                int totalHeader = ipH + tcpH;
                int totalSize = totalHeader + payload.Length;

                payload.CopyTo(tcpPacket[totalHeader..]);

                uint* pSeqPtr = (uint*)(pPacket + ipH + 4);
                uint* pAckPtr = (uint*)(pPacket + ipH + 8);

                uint currentSeq = BinaryPrimitives.ReverseEndianness(*pSeqPtr);
                uint currentAck = BinaryPrimitives.ReverseEndianness(*pAckPtr);

                ushort delta = (ushort)Math.Abs(tcpPacket.Length - totalSize);
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
    }
}
