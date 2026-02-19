using Caraota.NET.Common.Events;

using Caraota.NET.Core.Models.Views;

using Caraota.NET.Protocol.Stream;
using Caraota.NET.Protocol.Parsing;

using Caraota.NET.Infrastructure.Interception;

namespace Caraota.NET.Core.Session;

public sealed class MapleSession(IWinDivertSender winDivertSender) : IDisposable
{
    public event Action<Exception>? Error;
    public event PacketDecryptedDelegate? PacketDecrypted;
    public event HandshakeReceivedDelegate? HandshakeReceived;

    private readonly MapleStream _stream = new();
    private readonly MapleSessionManager _sessionManager = new(winDivertSender);

    public void ProcessPayload(WinDivertPacketViewEventArgs args, Span<byte> payload, long? parentId = null, int? parentReaded = null)
    {
        var iv = GetIV(args.IsIncoming);

        var packet = ParsePacket(payload, iv, args, parentId, parentReaded);
        var unifiedPayload = _stream.GetUnifiedPayload(packet.Id, payload, out int contLen);

        if (contLen > 0)
        {
            packet = ParsePacket(unifiedPayload, iv, args, packet.Id, packet.ParentReaded);
            packet.ContinuationLength = contLen;
        }

        if (_sessionManager.Initialize(args, packet, out var handshakePacketView))
        {
            HandshakeReceived?.Invoke(handshakePacketView); return;
        }

        if (!_sessionManager.Success) return;

        HandlePacketContinuation(packet);
        Decrypt(ref packet);

        PacketDecrypted?.Invoke(new MapleSessionViewEventArgs(args, packet));

        ProcessLeftovers(args, packet);
    }

    public void ProcessDecrypted(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        if (!_stream.IsFragment(packet))
        {
            EncryptAndSend(args);
            return;
        }

        Encrypt(ref packet);
        CopyToOutputBuffer(packet, GetOutputBuffer(packet));

        if (packet.Leftovers.IsEmpty)
        {
            FinalizeAndSend(args);
        }
    }

    private byte[]? GetIV(bool isIncoming)
        => isIncoming ? _sessionManager.Decryptor?.RIV : _sessionManager.Decryptor?.SIV;

    private static MaplePacketView ParsePacket(Span<byte> payload, byte[]? iv, WinDivertPacketViewEventArgs args, long? parentId, int? parentReaded)
        => PacketFactory.Parse(payload, iv, args.IsIncoming, args.Address.Timestamp, parentId, parentReaded);

    private void HandlePacketContinuation(MaplePacketView packet)
    {
        if (packet.RequiresContinuation)
        {
            _stream.SaveForContinuation(packet.Id, packet.Data);
        }
    }

    private void ProcessLeftovers(WinDivertPacketViewEventArgs args, MaplePacketView packet)
    {
        if (packet.Leftovers.Length == 0) return;

        int newOffset = packet.ParentReaded + packet.Header.Length + packet.Payload.Length - packet.ContinuationLength;
        ProcessPayload(args, packet.Leftovers, packet.Id, newOffset);
    }

    private MapleBuffer GetOutputBuffer(MaplePacketView packet) =>
    _stream.GetOrCreateBuffer(packet.Id, packet.TotalLength - packet.ContinuationLength, packet.IsIncoming);

    private static void CopyToOutputBuffer(MaplePacketView packet, MapleBuffer outBuffer)
    {
        packet.Data[packet.ContinuationLength..].CopyTo(outBuffer.AsSpan(packet.ParentReaded));
    }

    private void FinalizeAndSend(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        var finalData = _stream.Finalize(packet.Id, packet.IsIncoming);

        if (finalData is not null)
        {
            ReplaceAndAsend(finalData.Value.AsSpan(), args);
            _stream.CleanPayload(packet.Id);
        }

        packet.Release();
    }

    private void EncryptAndSend(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        Encrypt(ref packet);
        ReplaceAndAsend(packet.Data[packet.ContinuationLength..], args);

        packet.Release();
    }

    private void Encrypt(ref MaplePacketView packet)
    {
        _sessionManager.Encryptor.Encrypt(packet.Payload, packet.IsIncoming, packet.RequiresContinuation);
    }

    private void Decrypt(ref MaplePacketView packet)
    {
        _sessionManager.Decryptor.Decrypt(packet.Payload, packet.IsIncoming, packet.RequiresContinuation);
    }

    private void ReplaceAndAsend(ReadOnlySpan<byte> payload, MapleSessionViewEventArgs args)
    {
        winDivertSender.ReplaceAndSend(payload, args.DivertPacketView, args.Address);
    }

    public void Dispose()
    {
        _stream.Dispose();

        GC.SuppressFinalize(this);
    }
}
