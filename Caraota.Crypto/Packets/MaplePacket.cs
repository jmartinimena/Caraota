using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;

namespace Caraota.Crypto.Packets
{
    public class MaplePacket : IDisposable
    {
        private byte[]? _fullBuffer;

        private readonly long _timestamp = Stopwatch.GetTimestamp();

        public int DataLen { get; private set; }
        public int IvLen { get; private set; }
        public int HeaderLen { get; private set; }
        public int PayloadLen { get; private set; }

        public Memory<byte> Data => _fullBuffer.AsMemory(0, DataLen);
        public Memory<byte> IV => _fullBuffer.AsMemory(DataLen, IvLen);
        public Memory<byte> Header => _fullBuffer.AsMemory(DataLen + IvLen, HeaderLen);
        public Memory<byte> Payload => _fullBuffer.AsMemory(DataLen + IvLen + HeaderLen, PayloadLen);

        public ushort Opcode { get; private set; }
        public bool IsIncoming { get; private set; }

        public string IVStr => Convert.ToHexString(IV.Span);
        public string HeaderStr => Convert.ToHexString(Header.Span);
        public string PayloadStr => Convert.ToHexString(Payload.Span);
        public string ToHexString() => Convert.ToHexString(Data.Span);
        public string FormattedTime => GetRealTime(_timestamp).ToString("HH:mm:ss.fff");
        public MaplePacket(DecodedPacket maplePacket)
        {
            DataLen = maplePacket.Data.Length;
            IvLen = maplePacket.IV.Length;
            HeaderLen = maplePacket.Header.Length;
            PayloadLen = maplePacket.Payload.Length;

            int totalNeeded = DataLen + IvLen + HeaderLen + PayloadLen;

            _fullBuffer = ArrayPool<byte>.Shared.Rent(totalNeeded);

            maplePacket.Data.CopyTo(_fullBuffer.AsSpan(0, DataLen));
            maplePacket.IV.CopyTo(_fullBuffer.AsSpan(DataLen, IvLen));
            maplePacket.Header.CopyTo(_fullBuffer.AsSpan(DataLen + IvLen, HeaderLen));
            maplePacket.Payload.CopyTo(_fullBuffer.AsSpan(DataLen + IvLen + HeaderLen, PayloadLen));

            Opcode = BinaryPrimitives.ReadUInt16LittleEndian(_fullBuffer.AsSpan(DataLen + IvLen + HeaderLen, 2));

            IsIncoming = maplePacket.IsIncoming;
            _timestamp = Stopwatch.GetTimestamp();
        }

        public string Predict() => PacketUtils.Predict(Payload);

        public void Dispose()
        {
            if (_fullBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_fullBuffer);
                _fullBuffer = null;
            }
            GC.SuppressFinalize(this);
        }

        private static readonly DateTime _startTimeActual = DateTime.Now;
        private static readonly long _startTimeTimestamp = Stopwatch.GetTimestamp();

        public static DateTime GetRealTime(long packetTimestamp)
        {
            TimeSpan elapsedSinceStart = Stopwatch.GetElapsedTime(_startTimeTimestamp, packetTimestamp);
            return _startTimeActual + elapsedSinceStart;
        }
    }
}
