using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Caraota.NET.Common.Utils;
using Caraota.NET.IO;

namespace Caraota.NET.Core.Models.Views
{
    /// <summary>
    /// Representa una vista de alto rendimiento sobre un paquete de MapleStory.
    /// Al ser un 'ref struct', se asigna en el stack y permite manipulación 'inplace' 
    /// del buffer de red sin generar basura para el Garbage Collector (GC).
    /// </summary>
    public ref struct MaplePacketView
    {
        private int _readOffset;
        private int _writeOffset;

        private static long _counter = 0;
        /// <summary> Identificador único del paquete basado en el timestamp de alta resolución. </summary>
        public long Id { get; init; }

        /// <summary> Indica si el paquete proviene del servidor (true) o del cliente (false). </summary>
        public readonly bool IsIncoming { get; init; }

        /// <summary> Vista de los 4 bytes del header cifrado de MapleStory. </summary>
        public ReadOnlySpan<byte> Header { get; init; }
        /// <summary> Vista completa del paquete (Header + Payload) sin incluir leftovers. </summary>
        public Span<byte> Data { get; set; }

        /// <summary> Vista del Vector de Inicialización (IV) utilizado para este paquete específico. </summary>
        public Span<byte> IV { get; init; } = new byte[4];
        /// <summary> Segmento mutable que contiene el contenido del paquete. Se modifica directamente durante el cifrado/descifrado. </summary>
        public Span<byte> Payload { get; set; }

        /// <summary> Segmento que contiene datos adicionales en el buffer que no pertenecen a este paquete (fragmentación TCP). </summary>
        public Span<byte> Leftovers { get; set; }

        /// <summary> Offset acumulado de lectura en caso de paquetes reensamblados o recursivos. </summary>
        public int ParentReaded { get; set; }
        public int ContinuationLength { get; set; }
        public readonly bool RequiresContinuation { get; init; }

        /// <summary> Longitud total de la ventana de datos actual (Header + Payload + Leftovers). </summary>
        public readonly int TotalLength { get; init; }

        public long Timestamp { get; init; }

        /// <summary> Opcode del paquete, extraído de los primeros 2 bytes del Payload descifrado. </summary>
        public readonly ushort Opcode => BinaryPrimitives.ReadUInt16LittleEndian(Payload[..2]);


        /// <summary>
        /// Inicializa una nueva instancia de <see cref="MaplePacketView"/> segmentando el buffer original in-place.
        /// </summary>
        /// <param name="data">Buffer crudo capturado por WinDivert.</param>
        /// <param name="iv">IV actual de la sesión de criptografía.</param>
        /// <param name="isIncoming">Dirección del flujo de red.</param>
        /// <param name="parentId">ID del paquete padre en caso de fragmentación.</param>
        /// <param name="parentReaded">Offset de lectura heredado.</param>
        public MaplePacketView(Span<byte> data, ReadOnlySpan<byte> iv, bool isIncoming, long timestamp, long? parentId = null, int? parentReaded = null)
        {
            Id = parentId ?? Interlocked.Increment(ref _counter);
            ParentReaded = parentReaded ?? 0;
            IsIncoming = isIncoming;
            TotalLength = data.Length;
            Timestamp = timestamp;

            int dataLength = data.Length;
            int payloadLength = PacketUtils.GetLength(data);

            if (payloadLength > dataLength - 4)
            {
                RequiresContinuation = true;
                payloadLength = dataLength - 4;
            }

            int totalProcessed = payloadLength + 4;
            iv.CopyTo(IV);

            Header = data[..4];
            Payload = data.Slice(4, payloadLength);
            Data = data[..totalProcessed];
            Leftovers = data[totalProcessed..];
        }

        public T Read<T>(int? pos = null) where T : unmanaged
        {
            var result = LittleEndian.Read<T>(Payload[2..], pos ?? _readOffset);

            if (!pos.HasValue)
            {
                var size = Unsafe.SizeOf<T>();
                _readOffset += size;
            }

            return result;
        }

        public string ReadString(int? pos = null)
        {
            var result = LittleEndian.ReadString(Payload[2..], pos ?? _readOffset);

            if(!pos.HasValue)
            {
                var size = sizeof(ushort) + result.Length;
                _readOffset += size;
            }

            return result;
        }

        public void Write<T>(T value, int? pos = null) where T : unmanaged
        {
            LittleEndian.Write(Payload[2..], value, pos ?? _writeOffset);

            if(!pos.HasValue)
            {
                var size = Unsafe.SizeOf<T>();
                _writeOffset += size;
            }
        }

        public void WriteString(string value, int? pos = null)
        {
            Payload = LittleEndian.WriteString(Payload, value, pos ?? _writeOffset);

            if (!pos.HasValue)
            {
                var size = sizeof(ushort) + value.Length;
                _writeOffset += size;
            }
        }
    }
}
