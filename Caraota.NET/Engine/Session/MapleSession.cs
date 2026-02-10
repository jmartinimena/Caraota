using System.Runtime.CompilerServices;

using Caraota.Crypto.Packets;
using Caraota.NET.Common.Events;
using Caraota.NET.Infrastructure.TCP;
using Caraota.NET.Infrastructure.Interception;

namespace Caraota.NET.Engine.Session;

public interface ISessionState
{
    public bool Success { get; }
}

public sealed class MapleSession(IWinDivertSender winDivertSender) : ISessionState
{
    public event Action<MapleSessionViewEventArgs>? PacketDecrypted;

    private int startLen = 0;
    private readonly byte[] startBuffer = new byte[4096];
    private readonly byte[] payloadBuffer = new byte[32000];

    private readonly IWinDivertSender _winDivertSender = winDivertSender;
    private readonly IPacketReassembler _reassembler = new PacketReassembler();
    private readonly MapleSessionManager _sessionManager = new(winDivertSender);

    public bool Success => _sessionManager.Success;

    public HandshakeSessionPacket Initialize(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        => _sessionManager.Initialize(winDivertPacket, payload);

    public unsafe void ProcessRaw(WinDivertPacketViewEventArgs args, Span<byte> payload, long? parentId = null, int? parentReaded = null)
    {
        if (startLen > 0)
        {
            fixed (byte* pStart = startBuffer)
            fixed (byte* pPayloadBuf = payloadBuffer)
            fixed (byte* pIncoming = payload)
            {
                Buffer.MemoryCopy(pStart, pPayloadBuf, payloadBuffer.Length, startLen);
                Buffer.MemoryCopy(pIncoming, pPayloadBuf + startLen, payloadBuffer.Length - startLen, payload.Length);
            }

            payload = payloadBuffer.AsSpan(0, startLen + payload.Length);
            startLen = 0;
        }

        var decryptor = _sessionManager.Decryptor;
        var iv = args.IsIncoming ? decryptor.RIV : decryptor.SIV;
        var packet = PacketFactory.Parse(payload, iv, args.IsIncoming, parentId, parentReaded);

        if(startLen > 0)
        {
            packet.Rebuilt = true;
            startLen = 0;
        }

        if (packet.RequiresContinuation)
        {
            fixed(byte* pStart = startBuffer)
            fixed(byte* pData = packet.Data)
            {
                Buffer.MemoryCopy(pData, pStart, startBuffer.Length, startLen);
            }
        }

        decryptor.Decrypt(ref packet);
        PacketDecrypted?.Invoke(new MapleSessionViewEventArgs(args, packet));

        if (packet.Leftovers.Length == 0) return;

        int newOffset = packet.ParentReaded + packet.Header.Length + packet.Payload.Length;
        ProcessRaw(args, packet.Leftovers, packet.Id, newOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe void ProcessDecrypted(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        if (!_reassembler.IsFragment(packet))
        {
            EncryptAndSend(args);
            return;
        }

        byte[] outBuffer = _reassembler.GetOrCreateBuffer(packet.Id, packet.TotalLength, packet.IsIncoming);

        var encryptor = _sessionManager.Encryptor;
        encryptor.Encrypt(ref packet);

        fixed (byte* pSource = packet.Data)
        fixed (byte* pDest = outBuffer)
        {
            Buffer.MemoryCopy(
                pSource,
                pDest + packet.ParentReaded,
                outBuffer.Length - packet.ParentReaded,
                packet.Data.Length
            );
        }

        if (packet.Leftovers.Length == 0)
        {
            byte[]? finalData = _reassembler.Finalize(packet.Id, packet.IsIncoming);

            if (finalData != null)
            {
                _winDivertSender.ReplaceAndSend(args.WinDivertPacket, finalData, args.Address);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe void EncryptAndSend(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;
        var original = args.WinDivertPacket;
        var address = args.Address;

        _sessionManager.Encryptor.Encrypt(ref packet);
        _winDivertSender.ReplaceAndSend(original, packet.Data, address);
    }
}
