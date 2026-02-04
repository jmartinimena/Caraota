using System.Buffers;
using System.Buffers.Binary;

using Caraota.NET.Events;
using Caraota.NET.Interception;

namespace Caraota.NET.TCP
{
    public class TcpStackArchitect(IWinDivertSender sender)
    {
        private readonly IWinDivertSender _winDivertSender = sender;

        private ushort _fakeSeq = 0, _fakeAck = 0;

        private byte[] _tcpPacketBuffer = ArrayPool<byte>.Shared.Rent(65536);
        public void ModifyAndSend(MapleSessionEventArgs args, ReadOnlySpan<byte> maplePacket, bool isIncoming)
        {
            int ipH = (args.WinDivertPacket[0] & 0x0F) << 2;
            int tcpH = ((args.WinDivertPacket[ipH + 12] >> 4) & 0x0F) << 2;
            int totalHeader = ipH + tcpH;
            int totalSize = totalHeader + maplePacket.Length;

            Span<byte> newTcpSpan = _tcpPacketBuffer.AsSpan(0, totalSize);

            args.WinDivertPacket[..totalHeader].CopyTo(newTcpSpan[..totalHeader]);
            maplePacket.CopyTo(newTcpSpan[totalHeader..]);

            var delta = (ushort)Math.Abs(args.WinDivertPacket.Length - totalSize);
            uint finalSeq, finalAck;

            if (isIncoming)
            {
                finalSeq = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 4, 4)) + _fakeAck;
                finalAck = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 8, 4)) + _fakeSeq;
                _fakeAck += delta;
            }
            else
            {
                finalSeq = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 4, 4)) + _fakeSeq;
                finalAck = BinaryPrimitives.ReadUInt32BigEndian(newTcpSpan.Slice(ipH + 8, 4)) + _fakeAck;
                _fakeSeq += delta;
            }

            BinaryPrimitives.WriteUInt32BigEndian(newTcpSpan.Slice(ipH + 4, 4), finalSeq);
            BinaryPrimitives.WriteUInt32BigEndian(newTcpSpan.Slice(ipH + 8, 4), finalAck);
            BinaryPrimitives.WriteUInt16BigEndian(newTcpSpan.Slice(2, 2), (ushort)totalSize);

            ushort oldIpId = BinaryPrimitives.ReadUInt16BigEndian(newTcpSpan.Slice(4, 2));
            BinaryPrimitives.WriteUInt16BigEndian(newTcpSpan.Slice(4, 2), (ushort)(oldIpId + 1));

            _winDivertSender.SendPacket(newTcpSpan, args.Address);
        }
    }
}
