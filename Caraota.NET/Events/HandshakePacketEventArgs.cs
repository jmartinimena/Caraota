using Caraota.Crypto.Packets;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Caraota.NET.Events
{
    public readonly ref struct HandshakePacketEventArgs
    {
        public MapleSessionEventArgs MapleSessionEventArgs { get; }
        public ReadOnlySpan<byte> Packet { get; }
        public ReadOnlySpan<byte> Payload { get; }
        public readonly ushort Opcode { get; }
        public readonly ushort Version { get; }
        public ReadOnlySpan<byte> SIV { get; }
        public ReadOnlySpan<byte> RIV { get; }
        public readonly byte Locale { get; }

        public HandshakePacketEventArgs(MapleSessionEventArgs mapleSessionEventArgs, ReadOnlySpan<byte> packet)
        {
            MapleSessionEventArgs = mapleSessionEventArgs;
            Packet = packet;
            Payload = packet[2..];
            Opcode = BinaryPrimitives.ReadUInt16LittleEndian(packet[..2]);
            Version = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(2, 2));

            if (Version == 62)
            {
                SIV = Packet.Slice(6, 4);
                RIV = Packet.Slice(10, 4);
                Locale = Packet[14];
            }
            else
            {
                SIV = Packet.Slice(7, 4);
                RIV = Packet.Slice(11, 4);
                Locale = Packet[15];
            }
        }
    }

    public readonly struct HandshakeEventArgs
    {
        private readonly byte[]? _fullBuffer;
        public readonly int PayloadLen;
        public readonly int SIVLen;
        public readonly int RIVLen;
        public readonly ReadOnlyMemory<byte> Payload => _fullBuffer.AsMemory();
        public readonly ReadOnlyMemory<byte> SIV => _fullBuffer.AsMemory(PayloadLen, SIVLen);
        public readonly ReadOnlyMemory<byte> RIV => _fullBuffer.AsMemory(PayloadLen + SIVLen, RIVLen);
        public readonly ushort Opcode => BinaryPrimitives.ReadUInt16LittleEndian(Payload.Span[..2]);
        public readonly ushort Version => BinaryPrimitives.ReadUInt16LittleEndian(Payload.Span.Slice(2, 2));
        public readonly byte Locale => Payload.Span[14];

        private readonly long _timestamp = Stopwatch.GetTimestamp();
        public readonly string FormattedTime => PacketUtils.GetRealTime(_timestamp).ToString("HH:mm:ss:fff");

        public HandshakeEventArgs(HandshakePacketEventArgs args) 
        {
            PayloadLen = args.Packet.Length;
            SIVLen = args.Packet.Length;
            RIVLen = args.Packet.Length;

            int totalNeeded = PayloadLen + SIVLen + RIVLen;

            _fullBuffer = ArrayPool<byte>.Shared.Rent(totalNeeded);

            args.Packet.CopyTo(_fullBuffer.AsSpan(0, PayloadLen));
            args.SIV.CopyTo(_fullBuffer.AsSpan(PayloadLen, SIVLen));
            args.RIV.CopyTo(_fullBuffer.AsSpan(PayloadLen + SIVLen, RIVLen));
        }
    }
}
