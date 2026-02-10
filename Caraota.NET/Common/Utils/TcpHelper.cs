using System.Runtime.CompilerServices;

namespace Caraota.NET.Common.Utils
{
    public static class TcpHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryExtractPayload(Span<byte> tcpPacket, out Span<byte> payload)
        {
            fixed (byte* pBase = tcpPacket)
            {
                int ipH = (*pBase & 0x0F) << 2;

                int tcpH = ((*(pBase + ipH + 12) & 0xF0) >> 4) << 2;

                int offset = ipH + tcpH;
                int len = tcpPacket.Length - offset;

                if (len <= 0)
                {
                    payload = default;
                    return false;
                }

                payload = new Span<byte>(pBase + offset, len);
                return true;
            }
        }
    }
}
