using System.Buffers.Binary;

namespace Caraota.NET.Infrastructure.TCP
{
    public sealed class TcpStackArchitect
    {
        private ushort _fakeSeq = 0, _fakeAck = 0;
        public void ReplacePayload(Span<byte> tcpPacket, ReadOnlySpan<byte> payload, bool isIncoming)
        {
            //Console.WriteLine($"Original: {Convert.ToHexString(tcpPacket)}");

            int ipH = (tcpPacket[0] & 0x0F) << 2;
            int tcpH = ((tcpPacket[ipH + 12] >> 4) & 0x0F) << 2;
            int totalHeader = ipH + tcpH;
            int totalSize = totalHeader + payload.Length;


            payload.CopyTo(tcpPacket[totalHeader..]);

            var delta = (ushort)Math.Abs(tcpPacket.Length - totalSize);
            uint finalSeq, finalAck;

            if (isIncoming)
            {
                finalSeq = BinaryPrimitives.ReadUInt32BigEndian(tcpPacket.Slice(ipH + 4, 4)) + _fakeAck;
                finalAck = BinaryPrimitives.ReadUInt32BigEndian(tcpPacket.Slice(ipH + 8, 4)) + _fakeSeq;
                _fakeAck += delta;
            }
            else
            {
                finalSeq = BinaryPrimitives.ReadUInt32BigEndian(tcpPacket.Slice(ipH + 4, 4)) + _fakeSeq;
                finalAck = BinaryPrimitives.ReadUInt32BigEndian(tcpPacket.Slice(ipH + 8, 4)) + _fakeAck;
                _fakeSeq += delta;
            }

            BinaryPrimitives.WriteUInt32BigEndian(tcpPacket.Slice(ipH + 4, 4), finalSeq);
            BinaryPrimitives.WriteUInt32BigEndian(tcpPacket.Slice(ipH + 8, 4), finalAck);
            BinaryPrimitives.WriteUInt16BigEndian(tcpPacket.Slice(2, 2), (ushort)totalSize);

            //Console.WriteLine($"Construido: {Convert.ToHexString(newTcpSpan)}");
        }
    }
}
