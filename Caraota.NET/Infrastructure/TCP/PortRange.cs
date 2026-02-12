using System.Runtime.CompilerServices;

namespace Caraota.NET.Infrastructure.TCP
{
    public readonly struct PortRange(int start, int end)
    {
        public int Start { get; } = start;
        public int End { get; } = end;

        public static PortRange Any => default;
        public PortRange this[Range range]
        {
            get
            {
                int start = range.Start.Value;
                int end = range.End.Value;

                return new PortRange(start, end);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(int port)
        {
            bool isInRange = port >= Start && port <= End;

            return isInRange;
        }

        public override readonly string ToString() => $"{Start}-{End}";
    }
}
