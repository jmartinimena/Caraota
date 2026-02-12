using System.Runtime.CompilerServices;

namespace Caraota.NET.Common.Utils
{
    public static class TcpHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryExtractPayload(Span<byte> tcpPacket, out Span<byte> payload)
        {
            int ipH = (tcpPacket[0] & 0x0F) << 2;
            int tcpH = ((tcpPacket[ipH + 12] & 0xF0) >> 4) << 2;
            int offset = ipH + tcpH;

            if (offset >= tcpPacket.Length)
            {
                payload = default;
                return false;
            }

            payload = tcpPacket[offset..];
            return true;
        }
    }
}
