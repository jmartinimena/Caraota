using System.Reflection;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using WinDivertSharp;

using Caraota.NET.Common.Utils;
using Caraota.NET.Common.Events;
using Caraota.NET.Infrastructure.TCP;

namespace Caraota.NET.Infrastructure.Interception
{
    internal sealed class WinDivertWrapper : IWinDivertSender, IDisposable
    {
        public event Action<Exception>? Error;
        public event Action<WinDivertPacketViewEventArgs>? PacketReceived;

        private bool _isRunning;
        private Thread? _captureThread;

        private readonly IntPtr _handle;
        private readonly TcpStackArchitect _tcpStackArchitect;

        public WinDivertWrapper(string filter)
        {
            _tcpStackArchitect = new();

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
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };

            _captureThread.Start();
        }

        private void CaptureLoop()
        {
            Interop.SetThreadAffinity(4);

            WinDivertAddress address = default;
            using WinDivertBuffer buffer = new();

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
            PacketReceived!.Invoke(new WinDivertPacketViewEventArgs(buffer.AsSpan(len), address, false));
        }

        private uint _lastInboundSeq = 0;
        private unsafe void ProcessInboundPacket(WinDivertBuffer buffer, WinDivertAddress address, uint len)
        {
            byte* pBase = (byte*)buffer.GetPointer();

            int ipH = (*pBase & 0x0F) << 2;

            uint* pSeq = (uint*)(pBase + ipH + 4);
            uint currentSeq = BinaryPrimitives.ReverseEndianness(*pSeq);

            if (currentSeq == _lastInboundSeq)
            {
                SendPacket(new ReadOnlySpan<byte>(pBase, (int)len), address);
                return;
            }

            _lastInboundSeq = currentSeq;

            PacketReceived?.Invoke(new WinDivertPacketViewEventArgs(
                new Span<byte>(pBase, (int)len),
                address,
                true));
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
                    Error?.Invoke(new Win32Exception(error));
                    return;
            }
        }

        private readonly WinDivertBuffer _sendBuffer = new();
        public unsafe void SendPacket(ReadOnlySpan<byte> packet, WinDivertAddress address)
        {
            address.Impostor = true;

            WinDivert.WinDivertHelperCalcChecksums(_sendBuffer, (uint)packet.Length, ref address, WinDivertChecksumHelperParam.All);

            fixed (byte* ptr = packet)
            {
                _sendBuffer.SetBufferPointer((IntPtr)ptr);

                if (!WinDivert.WinDivertSend(_handle, _sendBuffer, (uint)packet.Length, ref address))
                    ThrowLastWin32Error();
            }
        }

        public void ReplaceAndSend(Span<byte> original, ReadOnlySpan<byte> payload, WinDivertAddress address)
        {
            bool isIncoming = address.Direction == WinDivertDirection.Inbound;
            _tcpStackArchitect.ReplacePayload(original, payload, isIncoming);
            SendPacket(original, address);
        }

        public void Stop()
        {
            _isRunning = false;

            if (_handle != IntPtr.Zero)
            {
                _sendBuffer.Dispose();
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
        public static Span<byte> AsSpan(this WinDivertBuffer buffer, uint length)
        {
            IntPtr ptr = BufferAccessor.GetPointer(buffer);
            return new Span<byte>(ptr.ToPointer(), (int)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<byte> AsMemory(this WinDivertBuffer buffer, uint length)
        {
            var array = BufferAccessor.GetArray(buffer);
            return new ReadOnlyMemory<byte>(array, 0, (int)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetBufferPointer(this WinDivertBuffer buffer, nint ptr)
        {
            BufferAccessor.SetBufferPointer(ptr, buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint GetPointer(this WinDivertBuffer buffer)
        {
            return BufferAccessor.GetPointer(buffer);
        }

        private static class BufferAccessor
        {
            private static readonly Func<WinDivertBuffer, IntPtr> _getPointerFunc;
            private static readonly Action<WinDivertBuffer, IntPtr> _setPointerFunc;
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

                var valueParam = Expression.Parameter(typeof(IntPtr));
                _setPointerFunc = Expression.Lambda<Action<WinDivertBuffer, IntPtr>>(
                Expression.Assign(Expression.Field(bufferParam, bufferPointerField), valueParam),
                bufferParam, valueParam).Compile();

                var arrayFieldAccess = Expression.Field(bufferParam, bufferArrayField);
                var arrayLambda = Expression.Lambda<Func<WinDivertBuffer, byte[]>>(
                    arrayFieldAccess, bufferParam);
                _getArrayFunc = arrayLambda.Compile();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static IntPtr GetPointer(WinDivertBuffer buffer) => _getPointerFunc(buffer);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static byte[] GetArray(WinDivertBuffer buffer) => _getArrayFunc(buffer);

            public static void SetBufferPointer(IntPtr ptr, WinDivertBuffer buffer)
            {
                _setPointerFunc(buffer, ptr);
            }
        }
    }

    internal static class WinDivertFactory
    {
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
