using System.Runtime.CompilerServices;

namespace Caraota.NET.Common.Utils
{
    public static class TcpHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryExtractPayload(ReadOnlySpan<byte> tcpPacket, out ReadOnlySpan<byte> payload)
        {
            int ipH = (tcpPacket[0] & 0x0F) << 2;
            int tcpH = ((tcpPacket[ipH + 12] & 0xF0) >> 4) << 2;

            int offset = ipH + tcpH;
            int len = tcpPacket.Length - offset;

            if (len <= 0) { payload = default; return false; }

            payload = tcpPacket.Slice(offset, len);
            return true;
        }
    }
}
