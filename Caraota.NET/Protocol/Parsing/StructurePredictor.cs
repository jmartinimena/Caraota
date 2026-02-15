using System.Text;
using System.Diagnostics;
using System.Buffers.Binary;

using Caraota.Crypto.State;
using Caraota.NET.Core.Models.Views;

namespace Caraota.NET.Protocol.Parsing
{
    public class StructurePredictor
    {
        private string _structure = string.Empty;
        private readonly ReadOnlyMemory<byte> _packet;

        public StructurePredictor(MaplePacketView packet)
        {
            _packet = packet.Payload.ToArray();

            PredictStructure();
        }

        public StructurePredictor(byte[] packet)
        {
            _packet = packet;

            PredictStructure();
        }

        public StructurePredictor(ReadOnlyMemory<byte> packet)
        {
            _packet = packet;

            PredictStructure();
        }

        public string GetStructure() => _structure;

        private class SubStructure
        {
            public int Previous { get; }
            public string TypeCode { get; }
            public double LogScore { get; }

            public SubStructure(int previous, string type, double logScore)
            {
                Previous = previous;
                TypeCode = type;
                LogScore = logScore;
            }
        }

        private void PredictStructure()
        {
            int dataLength = _packet.Length;

            SubStructure[] dynamic = new SubStructure[dataLength + 1];
            dynamic[0] = new SubStructure(-1, "", 0.0);

            var checkers = GetTypeCheckers();

            for (int i = 0; i < dataLength; i++)
            {
                if (dynamic[i] == null) continue;

                double currentLogScore = dynamic[i].LogScore;
                int absoluteIndex = i;

                foreach (var checker in checkers)
                {
                    if (!checker.CanRead(_packet, absoluteIndex)) continue;

                    double score = checker.GetScore(_packet, absoluteIndex);
                    double newScore = currentLogScore + Math.Log(score);

                    int nextPos = checker.NextIndex(_packet, absoluteIndex);

                    if (dynamic[nextPos] == null || newScore > dynamic[nextPos].LogScore)
                    {
                        dynamic[nextPos] = new SubStructure(i, checker.StructCode, newScore);
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            SubStructure current = dynamic[dataLength];

            if (current == null)
            {
                Debug.WriteLine($"[ERROR-PREDICTOR] No se pudo encontrar un camino válido para el paquete de longitud {dataLength}");
                _structure = "CORRUPTED";
                return;
            }

            while (current != null && current.Previous != -1)
            {
                int absolutePos = current.Previous;
                string formattedValue = GetFormattedValue(current.TypeCode, absolutePos);

                sb.Insert(0, formattedValue);
                current = dynamic[current.Previous];
            }

            _structure = sb.ToString();
        }

        private string GetFormattedValue(string typeCode, int index)
        {
            StringBuilder sb = new();
            ReadOnlySpan<byte> span = _packet.Span;

            switch (typeCode)
            {
                case "s": // STRING
                    ushort len = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(index, 2));
                    string text = Encoding.GetEncoding("ISO-8859-1").GetString(span.Slice(index + 2, len));
                    return $"[{span[index]}][{span[index + 1]}]{text}";

                case "i": // INT (4 bytes)
                    for (int i = 3; i >= 0; i--) AppendByteOrChar(sb, span[index + i]);
                    return sb.ToString();

                case "h": // SHORT (2 bytes)
                    for (int i = 1; i >= 0; i--) AppendByteOrChar(sb, span[index + i]);
                    return sb.ToString();

                case "b": // BYTE
                    AppendByteOrChar(sb, span[index]);
                    return sb.ToString();

                default:
                    return $"[{span[index]}]";
            }
        }

        private static void AppendByteOrChar(StringBuilder sb, byte b)
        {
            if ((b >= 32 && b <= 126) || (b >= 160 && b <= 255))
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append($"[{b}]");
            }
        }

        private List<TypeChecker> GetTypeCheckers() => 
        [
            new StringChecker(),
            new IntChecker(),
            new ShortChecker(),
            new ByteChecker()
        ];

        // --- CLASES DE VALIDACIÓN (TYPE CHECKERS) ---

        private abstract class TypeChecker
        {
            public abstract string StructCode { get; }
            public abstract bool CanRead(ReadOnlyMemory<byte> data, int index);
            public abstract int NextIndex(ReadOnlyMemory<byte> data, int index);
            public abstract double GetScore(ReadOnlyMemory<byte> data, int index);
        }

        // --- CLASES DE VALIDACIÓN (TYPE CHECKERS) ---

        private class IntChecker : TypeChecker
        {
            public override string StructCode => "i";
            public override bool CanRead(ReadOnlyMemory<byte> data, int index)
            {
                return index >= 2 && (data.Length - index >= 4);
            }
            public override int NextIndex(ReadOnlyMemory<byte> data, int index) => index + 4;
            public override double GetScore(ReadOnlyMemory<byte> data, int index) => 2.0;
        }

        private class ShortChecker : TypeChecker
        {
            public override string StructCode => "h";
            public override bool CanRead(ReadOnlyMemory<byte> data, int index)
            {
                return index != 1 && (data.Length - index >= 2);
            }
            public override int NextIndex(ReadOnlyMemory<byte> data, int index) => index + 2;
            public override double GetScore(ReadOnlyMemory<byte> data, int index)
            {
                return (index == 0) ? 5.0 : 1.2;
            }
        }

        private class StringChecker : TypeChecker
        {
            public override string StructCode => "s";
            public override bool CanRead(ReadOnlyMemory<byte> data, int index)
            {
                if (index < 2 || data.Length - index < 2) return false;

                ushort len = BinaryPrimitives.ReadUInt16LittleEndian(data.Span.Slice(index, 2));
                return len > 0 && len <= (data.Length - index - 2) && IsPrintable(data.Span.Slice(index + 2, len));
            }
            public override int NextIndex(ReadOnlyMemory<byte> data, int index)
            {
                ushort len = BinaryPrimitives.ReadUInt16LittleEndian(data.Span.Slice(index, 2));
                return index + 2 + len;
            }
            public override double GetScore(ReadOnlyMemory<byte> data, int index) => 100.0;
        }

        private class ByteChecker : TypeChecker
        {
            public override string StructCode => "b";
            public override bool CanRead(ReadOnlyMemory<byte> data, int index) => data.Length - index >= 1;
            public override int NextIndex(ReadOnlyMemory<byte> data, int index) => index + 1;
            public override double GetScore(ReadOnlyMemory<byte> data, int index) => 1.0;
        }

        private static bool IsPrintable(ReadOnlySpan<byte> span)
        {
            foreach (byte b in span) if (!IsPrintable(b)) return false;
            return true;
        }
        private static bool IsPrintable(byte b) => (b >= 32 && b <= 126) || (b >= 160 && b <= 255);
    }
}
