using System.Runtime.CompilerServices;

using Caraota.NET.Common.Events;

using Caraota.NET.Protocol.Stream;
using Caraota.NET.Protocol.Structures;

using Caraota.NET.Infrastructure.TCP;
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

    private readonly MapleStream _stream = new();
    private readonly PacketReassembler _reassembler = new();
    private readonly MapleSessionManager _sessionManager = new(winDivertSender);

    private readonly IWinDivertSender _winDivertSender = winDivertSender;

    public bool Success => _sessionManager.Success;

    public bool Initialize(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload, out HandshakePacketView handshakePacketView)
        => _sessionManager.Initialize(winDivertPacket, payload, out handshakePacketView);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ProcessRaw(WinDivertPacketViewEventArgs args, Span<byte> payload, long? parentId = null, int? parentReaded = null)
    {
        var decryptor = _sessionManager.Decryptor;

        var iv = args.IsIncoming ? decryptor?.RIV : decryptor?.SIV;
        var packet = PacketFactory.Parse(payload, iv, args.IsIncoming, parentId, parentReaded);

        var unifiedPayload = _stream.GetUnifiedPayload(packet.Id, payload, out int contLen);

        if (contLen > 0)
        {
            packet = PacketFactory.Parse(unifiedPayload, iv, args.IsIncoming, packet.Id, packet.ParentReaded);
            packet.ContinuationLength = contLen;
        }


        if (packet.Opcode == 0 && Initialize(args, payload, out var handshakePacketView))
        {
            HandshakeReceived?.Invoke(new HandshakeEventArgs(handshakePacketView));
            return;
        }

        if (packet.RequiresContinuation)
        {
            _stream.SaveForContinuation(packet.Id, packet.Data);
        }

        decryptor!.Decrypt(ref packet);
        PacketDecrypted?.Invoke(new MapleSessionViewEventArgs(args, packet));

        if (packet.Leftovers.Length == 0) return;

        int newOffset = packet.ParentReaded + packet.Header.Length + packet.Payload.Length - packet.ContinuationLength;
        ProcessRaw(args, packet.Leftovers, packet.Id, newOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ProcessDecrypted(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        if (!_reassembler.IsFragment(packet))
        {
            EncryptAndSend(args);
            return;
        }

        var outBuffer = _reassembler.GetOrCreateBuffer(packet.Id, packet.TotalLength - packet.ContinuationLength, packet.IsIncoming);

        var encryptor = _sessionManager.Encryptor;
        encryptor.Encrypt(ref packet);

        packet.Data[packet.ContinuationLength..].CopyTo(outBuffer.AsSpan(packet.ParentReaded));

        if (packet.Leftovers.Length == 0)
        {
            var finalData = _reassembler.Finalize(packet.Id, packet.IsIncoming);

            if (finalData is null) return;

            if (finalData != null)
            {
                _winDivertSender.ReplaceAndSend(args.DivertPacketView, finalData.Value.AsSpan(), args.Address);
                _stream.CleanPayload(packet.Id);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EncryptAndSend(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;
        var original = args.DivertPacketView;
        var address = args.Address;

        _sessionManager.Encryptor.Encrypt(ref packet);
        _winDivertSender.ReplaceAndSend(original, packet.Data[packet.ContinuationLength..], address);
    }
}
