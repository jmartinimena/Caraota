using System.Reflection;
using System.Diagnostics;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using WinDivertSharp;

using Caraota.NET.Events;
using Caraota.NET.Utils;

namespace Caraota.NET.Interception
{
    internal sealed class WinDivertWrapper : IWinDivertSender, IDisposable
    {
        public delegate void PacketEventHandler(WinDivertPacketEventArgs args);

        public event PacketEventHandler? OnOutboundPacket;
        public event PacketEventHandler? OnInboundPacket;
        public event Action<Exception>? OnError;

        private bool _isRunning;
        private Thread? _captureThread;

        private readonly IntPtr _handle;

        public WinDivertWrapper(string filter)
        {
            _handle = WinDivert.WinDivertOpen(
                filter,
                WinDivertLayer.Network,
                0,
                WinDivertOpenFlags.None
            );

            WinDivert.WinDivertSetParam(_handle, WinDivertParam.QueueLen, 8192);
            WinDivert.WinDivertSetParam(_handle, WinDivertParam.QueueTime, 1000);

            if (_handle == IntPtr.Zero)
                ThrowLastWin32Error();
        }

        private static void ThrowLastWin32Error()
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _captureThread = new Thread(CaptureLoop)
            {
                Name = "WinDivert-Capture",
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };

            _captureThread.Start();
        }

        private void CaptureLoop()
        {
            // Evitamos el nucleo 0 porque es donde Windows suele meter toda la carga del sistema
            Interop.SetThreadAffinity(2);

            WinDivertAddress address = default;
            WinDivertBuffer buffer = new();

            while (_isRunning)
            {
                uint readLen = 0;
                if (WinDivert.WinDivertRecv(_handle, buffer, ref address, ref readLen))
                {
                    ProcessPacket(buffer, address, readLen);
                }
                else
                {
                    HandleWinDivertError();
                    break;
                }
            }

            buffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool IsOutbound(WinDivertAddress address)
            => address.Direction == WinDivertDirection.Outbound;

        private void ProcessPacket(WinDivertBuffer buffer, WinDivertAddress address, uint len)
        {
            if (IsOutbound(address))
                ProcessOutboundPacket(buffer, address, len);
            else
                ProcessInboundPacket(buffer, address, len);
        }

        private void ProcessOutboundPacket(WinDivertBuffer buffer, WinDivertAddress address, uint len)
        {
            OnOutboundPacket!.Invoke(new WinDivertPacketEventArgs(buffer.AsSpan(len), address));
        }

        private uint _lastInboundSeq = 0;
        private void ProcessInboundPacket(WinDivertBuffer buffer, WinDivertAddress address, uint len)
        {
            var span = buffer.AsSpan(len);
            int ipH = (span[0] & 0x0F) << 2;
            uint currentSeq = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(ipH + 4, 4));

            if (currentSeq == _lastInboundSeq)
            {
                SendPacket(span, address);
                return;
            }

            _lastInboundSeq = currentSeq;

            OnInboundPacket!.Invoke(new WinDivertPacketEventArgs(span, address));
        }

        private void HandleWinDivertError()
        {
            int error = Marshal.GetLastWin32Error();

            switch (error)
            {
                case 995:
                    _isRunning = false;
                    return;

                case 997:
                    return;

                default:
                    OnError?.Invoke(new Win32Exception(error));
                    return;
            }
        }

        private readonly WinDivertBuffer _sendBuffer = new();
        public void SendPacket(ReadOnlySpan<byte> packet, WinDivertAddress address, bool log = false)
        {
            if (packet.Length <= 0) return;

            packet.CopyToWinDivertBuffer(_sendBuffer, packet.Length);

            WinDivert.WinDivertHelperCalcChecksums(_sendBuffer, (uint)packet.Length, ref address, WinDivertChecksumHelperParam.All);

            if (log)
                Debug.WriteLine($"Construido: {Convert.ToHexString(_sendBuffer.AsSpan((uint)packet.Length))}");

            if (!WinDivert.WinDivertSend(_handle, _sendBuffer, (uint)packet.Length, ref address))
                ThrowLastWin32Error();
        }

        public void Stop()
        {
            _isRunning = false;

            if (_handle != IntPtr.Zero)
            {
                WinDivert.WinDivertClose(_handle);
            }

            _captureThread?.Join();
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }

    internal static unsafe class WinDivertBufferExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ReadOnlySpan<byte> AsSpan(this WinDivertBuffer buffer, uint length)
        {
            IntPtr ptr = BufferAccessor.GetPointer(buffer)!;
            return new ReadOnlySpan<byte>(ptr.ToPointer(), (int)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<byte> AsMemory(this WinDivertBuffer buffer, uint length)
        {
            var array = BufferAccessor.GetArray(buffer);
            return new ReadOnlyMemory<byte>(array, 0, (int)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static unsafe void CopyToWinDivertBuffer(
            this ReadOnlySpan<byte> source,
            WinDivertBuffer destination,
            int length)
        {
            IntPtr dstPtr = BufferAccessor.GetPointer(destination)!;

            ref byte srcRef = ref MemoryMarshal.GetReference(source);
            ref byte dstRef = ref *(byte*)dstPtr;

            Unsafe.CopyBlock(ref dstRef, ref srcRef, (uint)length);
        }

        private static class BufferAccessor
        {
            private static readonly Func<WinDivertBuffer, IntPtr> _getPointerFunc;
            private static readonly Func<WinDivertBuffer, byte[]> _getArrayFunc;

            static BufferAccessor()
            {
                var bufferPointerField = typeof(WinDivertBuffer)
                    .GetField("BufferPointer", BindingFlags.Instance | BindingFlags.NonPublic)!;

                var bufferArrayField = typeof(WinDivertBuffer)
                    .GetField("_buffer", BindingFlags.Instance | BindingFlags.NonPublic)!;

                // Delegate para BufferPointer (IntPtr)
                var bufferParam = Expression.Parameter(typeof(WinDivertBuffer));
                var pointerFieldAccess = Expression.Field(bufferParam, bufferPointerField);
                var pointerLambda = Expression.Lambda<Func<WinDivertBuffer, IntPtr>>(
                    pointerFieldAccess, bufferParam);
                _getPointerFunc = pointerLambda.Compile();

                // Delegate para _buffer (byte[])
                var arrayFieldAccess = Expression.Field(bufferParam, bufferArrayField);
                var arrayLambda = Expression.Lambda<Func<WinDivertBuffer, byte[]>>(
                    arrayFieldAccess, bufferParam);
                _getArrayFunc = arrayLambda.Compile();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static IntPtr GetPointer(WinDivertBuffer buffer) => _getPointerFunc(buffer);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static byte[] GetArray(WinDivertBuffer buffer) => _getArrayFunc(buffer);
        }
    }

    internal static class WinDivertFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WinDivertWrapper CreateForPort(int port, bool outbound = true, bool inbound = true)
        {
            string filter;

            if (outbound && inbound)
            {
                filter = $"(tcp.DstPort == {port} or tcp.SrcPort == {port})";
            }
            else if (outbound)
            {
                filter = $"tcp.DstPort == {port}";
            }
            else
            {
                filter = $"tcp.SrcPort == {port}";
            }

            return new WinDivertWrapper($"tcp.PayloadLength > 0 and ({filter}) and !loopback and !impostor");
        }

        internal static WinDivertWrapper CreateForTcpPort(int port)
            => CreateForPort(port, true, true);
    }
}
