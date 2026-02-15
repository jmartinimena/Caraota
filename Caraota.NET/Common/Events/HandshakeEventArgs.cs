using System.Buffers;
using System.Diagnostics;
using System.Buffers.Binary;

using Caraota.Crypto.Utils;

using Caraota.NET.Infrastructure.Interception;

namespace Caraota.NET.Common.Events
{
    public readonly ref struct HandshakePacketView
    {
        public readonly int SIVOffset = 6;
        public readonly int RIVOffset = 10;
        public readonly int LocaleOffset = 14;
        public MapleSessionViewEventArgs MapleSessionEventArgs { get; }
        public ReadOnlySpan<byte> Packet { get; }
        public ReadOnlySpan<byte> SIV { get; }
        public ReadOnlySpan<byte> RIV { get; }
        public long Timestamp { get; }

        public HandshakePacketView(MapleSessionViewEventArgs mapleSessionEventArgs, ReadOnlySpan<byte> packet)
        {
            Packet = packet;
            MapleSessionEventArgs = mapleSessionEventArgs;
            Timestamp = mapleSessionEventArgs.Address.Timestamp;

            var version = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(2, 2));
            if (version != 62)
            {
                SIVOffset++;
                RIVOffset++;
                LocaleOffset++;
            }

            SIV = Packet.Slice(SIVOffset, 4);
            RIV = Packet.Slice(RIVOffset, 4);
        }
    }

    public readonly struct HandshakeEventArgs : IDisposable
    {
        private readonly byte[] _fullBuffer;

        private const int _sivLen = 4;
        private const int _rivLen = 4;
        private readonly int _dataLen;
        private readonly int _sivOffset;
        private readonly int _rivOffset;
        private readonly int _localeOffset;

        public readonly ushort Opcode;
        private readonly long Timestamp;
        public readonly ReadOnlyMemory<byte> Payload => _fullBuffer.AsMemory(0, _dataLen);
        public readonly ReadOnlyMemory<byte> SIV => _fullBuffer.AsMemory(_sivOffset, _sivLen);
        public readonly ReadOnlyMemory<byte> RIV => _fullBuffer.AsMemory(_rivOffset, _rivLen);
        public readonly ushort Version => BinaryPrimitives.ReadUInt16LittleEndian(Payload.Span[2..]);
        public readonly ushort SubVersion => BinaryPrimitives.ReadUInt16LittleEndian(Payload.Span[4..]);
        public readonly byte Locale => Payload.Span[_localeOffset];

        public readonly string FormattedTime => PacketUtils.GetRealTime(Timestamp).ToString("HH:mm:ss:fff");

        public HandshakeEventArgs(HandshakePacketView args)
        {
            _dataLen = args.Packet.Length;
            _sivOffset = args.SIVOffset;
            _rivOffset = args.RIVOffset;
            _localeOffset = args.LocaleOffset;

            _fullBuffer = ArrayPool<byte>.Shared.Rent(_dataLen);

            args.Packet.CopyTo(_fullBuffer.AsSpan(0, _dataLen));

            Timestamp = args.Timestamp;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_fullBuffer);
            GC.SuppressFinalize(this);
        }
    }
}
