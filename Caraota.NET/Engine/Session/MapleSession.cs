using System.Runtime.CompilerServices;

using Caraota.NET.Common.Events;
using Caraota.NET.Infrastructure.TCP;
using Caraota.NET.Protocol.Structures;
using Caraota.NET.Infrastructure.Interception;

namespace Caraota.NET.Engine.Session;

public interface ISessionState
{
    public bool Success { get; }
}

public sealed class MapleSession(IWinDivertSender winDivertSender) : ISessionState
{
    public event Action<HandshakeEventArgs>? HandshakeReceived;
    public event Action<MapleSessionViewEventArgs>? PacketDecrypted;

    private int startLen = 0;
    private readonly byte[] startBuffer = new byte[4096];
    private readonly byte[] payloadBuffer = new byte[32000];

    private readonly IWinDivertSender _winDivertSender = winDivertSender;
    private readonly IPacketReassembler _reassembler = new PacketReassembler();
    private readonly MapleSessionManager _sessionManager = new(winDivertSender);

    public bool Success => _sessionManager.Success;

    public bool Initialize(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload, out HandshakePacketView handshakePacketView)
        => _sessionManager.Initialize(winDivertPacket, payload, out handshakePacketView);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe void ProcessRaw(WinDivertPacketViewEventArgs args, Span<byte> payload, long? parentId = null, int? parentReaded = null)
    {
        if (startLen > 0)
        {
            Span<byte> destination = payloadBuffer.AsSpan(0, startLen + payload.Length);
            startBuffer.AsSpan(0, startLen).CopyTo(destination);
            payload.CopyTo(destination[startLen..]);
            payload = destination;
        }

        var decryptor = _sessionManager.Decryptor;
        var iv = args.IsIncoming ? decryptor.RIV : decryptor.SIV;
        var packet = PacketFactory.Parse(payload, iv, args.IsIncoming, parentId, parentReaded);

        if (packet.Opcode == 0)
        {
            if (Initialize(args, payload, out var handshakePacketView))
                HandshakeReceived?.Invoke(new HandshakeEventArgs(handshakePacketView));

            return;
        }

        if (startLen > 0)
        {
            packet.ContinuationLength = startLen;
            startLen = 0;
        }

        if (packet.RequiresContinuation)
        {
            startLen = packet.Data.Length;
            packet.Data.CopyTo(startBuffer);
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

        packet.Data.CopyTo(outBuffer.AsSpan(packet.ParentReaded));

        if (packet.Leftovers.Length == 0)
        {
            byte[]? finalData = _reassembler.Finalize(packet.Id, packet.IsIncoming);

            if (finalData != null)
            {
                _winDivertSender.ReplaceAndSend(args.WinDivertPacket, finalData.AsSpan(packet.ContinuationLength), args.Address);
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
        _winDivertSender.ReplaceAndSend(original, packet.Data[packet.ContinuationLength..], address);
    }
}
