using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Caraota.NET.Infrastructure.TCP
{
    public sealed class TcpStackArchitect
    {
        private ushort _fakeSeq = 0, _fakeAck = 0;
        public unsafe void ReplacePayload(Span<byte> tcpPacket, ReadOnlySpan<byte> payload, bool isIncoming)
        {
            //Console.WriteLine($"Original: {Convert.ToHexString(tcpPacket)}");

            fixed (byte* pPacket = tcpPacket)
            {
                fixed (byte* pPayload = payload)
                {
                    int ipH = (*pPacket & 0x0F) << 2;

                    int tcpH = ((*(pPacket + ipH + 12) >> 4) & 0x0F) << 2;

                    int totalHeader = ipH + tcpH;
                    int totalSize = totalHeader + payload.Length;

                    Unsafe.CopyBlock(pPacket + totalHeader, pPayload, (uint)payload.Length);

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

            //Console.WriteLine($"Construido: {Convert.ToHexString(newTcpSpan)}");
        }
    }
}
