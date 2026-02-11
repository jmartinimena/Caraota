using Caraota.NET.Infrastructure.Interception;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using WinDivertSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

            Console.WriteLine($"Construido: {Convert.ToHexString(tcpPacket)}");
        }

        public unsafe (byte[] a, byte[] b) Split(Span<byte> tcpPacket)
        {
            fixed (byte* pFullData = tcpPacket)
            {
                int ipHeaderLen = (pFullData[0] & 0x0F) * 4;
                int tcpHeaderLen = ((pFullData[ipHeaderLen + 12] >> 4) & 0x0F) * 4;
                int totalHeaderLen = ipHeaderLen + tcpHeaderLen;

                // Dividimos el payload en dos partes
                int totalPayload = (int)tcpPacket.Length - totalHeaderLen;

                // Decidimos un punto de corte seguro (MTU 1500 - Headers)
                int maxSegmentSize = 1460 - totalHeaderLen;
                int firstPayloadLen = maxSegmentSize;
                int secondPayloadLen = totalPayload - firstPayloadLen;

                // --- PAQUETE 1 ---
                byte[] part1 = new byte[totalHeaderLen + firstPayloadLen];
                // Copiamos headers y la primera parte del payload
                tcpPacket.Slice(0, totalHeaderLen + firstPayloadLen).CopyTo(part1);
                fixed (byte* p1 = part1)
                {
                    ushort len1 = (ushort)part1.Length;
                    p1[2] = (byte)(len1 >> 8); p1[3] = (byte)(len1 & 0xFF);
                }

                byte[] part2 = new byte[totalHeaderLen + secondPayloadLen];
                tcpPacket.Slice(0, totalHeaderLen).CopyTo(part2);
                tcpPacket.Slice(totalHeaderLen + firstPayloadLen, secondPayloadLen).CopyTo(part2.AsSpan(totalHeaderLen));

                fixed (byte* p2 = part2)
                {
                    ushort len2 = (ushort)part2.Length;
                    p2[2] = (byte)(len2 >> 8);
                    p2[3] = (byte)(len2 & 0xFF);

                    // AJUSTE CRÍTICO: El Sequence Number de TCP debe aumentar
                    // Offset en TCP Header para Sequence Number: 4 bytes después del inicio de TCP
                    byte* pTcpHeader2 = p2 + ipHeaderLen;
                    uint oldSeq = (uint)((pTcpHeader2[4] << 24) | (pTcpHeader2[5] << 16) | (pTcpHeader2[6] << 8) | pTcpHeader2[7]);
                    uint newSeq = oldSeq + (uint)firstPayloadLen;

                    pTcpHeader2[4] = (byte)(newSeq >> 24);
                    pTcpHeader2[5] = (byte)(newSeq >> 16);
                    pTcpHeader2[6] = (byte)(newSeq >> 8);
                    pTcpHeader2[7] = (byte)(newSeq & 0xFF);
                }

                return (part1, part2);
            }
        }
    }
}
