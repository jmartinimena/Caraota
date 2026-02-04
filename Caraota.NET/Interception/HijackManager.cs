using Caraota.NET.Events;

using Caraota.Crypto.Packets;
using Caraota.Crypto.Processing;

namespace Caraota.NET.Interception
{
    public class HijackManager
    {
        private readonly Queue<MaplePacket> _inHijackQueue = new();
        private readonly Queue<MaplePacket> _outHijackQueue = new();

        private static readonly byte[] HijackPattern = [.. "3A68696A61636B" // :hijack
            .Chunk(2).Select(s => Convert.ToByte(new string(s), 16))];
        internal void ProcessQueue(ref MapleSessionEventArgs args, bool isIncoming)
        {
            var hijackQueue = isIncoming ? _inHijackQueue : _outHijackQueue;
            if (hijackQueue.Count == 0) return;

            if (args.DecodedPacket.Opcode == (isIncoming ? 122 : 46) &&
                args.DecodedPacket.Data.IndexOf(HijackPattern) != -1)
            {
                args.DecodedPacket = PacketFactory.Parse(hijackQueue.Dequeue());
                args.Hijacked = true;
            }
        }

        public void HijackOnServer(MaplePacket packet)
            => _outHijackQueue.Enqueue(packet);

        public void HijackOnClient(MaplePacket packet)
            => _inHijackQueue.Enqueue(packet);
    }
}
