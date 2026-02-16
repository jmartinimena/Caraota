using Caraota.NET.Common.Utils;
using Caraota.NET.Core.Models.Views;
using System.Buffers.Binary;

namespace Caraota.NET.Common.Events
{
    public readonly ref struct HandshakePacketViewEventArgs
    {
        private readonly int _sivOffset = 6;
        private readonly int _rivOffset = 10;
        private readonly int _localeOffset = 14;
        public MapleSessionViewEventArgs MapleSessionEventArgs { get; }
        public ushort Opcode { get; }
        public ReadOnlySpan<byte> Data { get; }
        public ReadOnlySpan<byte> SIV { get; }
        public ReadOnlySpan<byte> RIV { get; }
        public ushort Version { get; }
        public ushort Subversion { get; }
        public byte Locale { get; }
        public long Timestamp { get; }
        public readonly string FormattedTime => PacketUtils.GetRealTime(Timestamp).ToString("HH:mm:ss:fff");

        public HandshakePacketViewEventArgs(MapleSessionViewEventArgs mapleSessionEventArgs)
        {
            var packet = mapleSessionEventArgs.MaplePacketView;

            Data = packet.Data;
            Opcode = packet.Opcode;
            MapleSessionEventArgs = mapleSessionEventArgs;
            Timestamp = mapleSessionEventArgs.Address.Timestamp;

            Version = packet.Read<ushort>(2);
            if (Version != 62)
            {
                _sivOffset++;
                _rivOffset++;
                _localeOffset++;
            }

            Subversion = packet.Read<ushort>(3);
            SIV = packet.ReadBytes(_sivOffset, 4);
            RIV = packet.ReadBytes(_rivOffset, 4);
            Locale = packet.Read<byte>(_localeOffset);
        }
    }
}
