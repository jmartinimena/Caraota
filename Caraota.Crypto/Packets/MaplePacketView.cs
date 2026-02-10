using System.Diagnostics;
using System.Buffers.Binary;

namespace Caraota.Crypto.Packets
{
    /// <summary>
    /// Representa una vista de alto rendimiento sobre un paquete de MapleStory.
    /// Al ser un 'ref struct', se asigna en el stack y permite manipulación 'inplace' 
    /// del buffer de red sin generar basura para el Garbage Collector (GC).
    /// </summary>
    public ref struct MaplePacketView
    {
        /// <summary> Identificador único del paquete basado en el timestamp de alta resolución. </summary>
        public long Id { get; set; }

        /// <summary> Indica si el paquete proviene del servidor (true) o del cliente (false). </summary>
        public readonly bool IsIncoming { get; init; }

        /// <summary> Vista del Vector de Inicialización (IV) utilizado para este paquete específico. </summary>
        public ReadOnlySpan<byte> IV { get; init; }

        /// <summary> Vista completa del paquete (Header + Payload) sin incluir leftovers. </summary>
        public ReadOnlySpan<byte> Data { get; init; }

        /// <summary> Vista de los 4 bytes del header cifrado de MapleStory. </summary>
        public ReadOnlySpan<byte> Header { get; init; }

        /// <summary> Segmento mutable que contiene el contenido del paquete. Se modifica directamente durante el cifrado/descifrado. </summary>
        public Span<byte> Payload { get; init; }

        /// <summary> Segmento que contiene datos adicionales en el buffer que no pertenecen a este paquete (fragmentación TCP). </summary>
        public Span<byte> Leftovers { get; init; }

        /// <summary> Offset acumulado de lectura en caso de paquetes reensamblados o recursivos. </summary>
        public int ParentReaded { get; set; }
        public bool Rebuilt { get; set; }
        public readonly bool RequiresContinuation { get; init; }

        /// <summary> Longitud total de la ventana de datos actual (Header + Payload + Leftovers). </summary>
        public readonly int TotalLength => Header.Length + Payload.Length + Leftovers.Length;

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
        public MaplePacketView(Span<byte> data, ReadOnlySpan<byte> iv, bool isIncoming, long? parentId = null, int? parentReaded = null)
        {
            Id = parentId ?? Stopwatch.GetTimestamp();
            ParentReaded = parentReaded ?? 0;
            IsIncoming = isIncoming;

            // Extraer longitud del payload desde el header (primeros 4 bytes)
            ReadOnlySpan<byte> header = data[..4];
            int payloadLength = PacketUtils.GetLength(header);
            header = PacketUtils.GetHeader(iv, payloadLength, isIncoming);

            // Seguridad: Evitar OutOfMemory si el header reporta más de lo que hay en el buffer actual
            if (payloadLength > data.Length - 4)
            {
                RequiresContinuation = true;
                payloadLength = data.Length - 4;
            }

            int totalProcessed = payloadLength + 4;

            // No podemos escapar de este caso, como el IV rota justo despues del descifrado
            // Debemos alocarlo para no perder el IV que descifro el paquete, duele, pero igual es un costo menor
            Span<byte> bufferIv = new byte[iv.Length];
            iv.CopyTo(bufferIv);

            // Asignación de vistas (Spans) sobre el mismo segmento de memoria original
            IV = bufferIv;
            Header = header;
            Payload = data.Slice(4, payloadLength);
            Data = data[..totalProcessed];
            Leftovers = data[totalProcessed..];
        }
    }
}
