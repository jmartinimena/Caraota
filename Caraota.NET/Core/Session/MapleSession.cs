using Caraota.NET.Protocol.Stream;
using Caraota.NET.Protocol.Parsing;

using Caraota.NET.Common.Events;
using Caraota.NET.Common.Exceptions;

using Caraota.NET.Core.Models.Views;

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
        var decryptor = _sessionManager.Decryptor;

        var iv = args.IsIncoming ? decryptor?.RIV : decryptor?.SIV;
        var packet = PacketFactory.Parse(payload, iv, args.IsIncoming, args.Address.Timestamp, parentId, parentReaded);

        var unifiedPayload = _stream.GetUnifiedPayload(packet.Id, payload, out int contLen);

        if (contLen > 0)
        {
            packet = PacketFactory.Parse(unifiedPayload, iv, args.IsIncoming, args.Address.Timestamp, packet.Id, packet.ParentReaded);
            packet.ContinuationLength = contLen;
        }


        if ((packet.Opcode == 0 || packet.Opcode == 1) && _sessionManager.Initialize(args, payload, out var handshakePacketView))
        {
            HandshakeReceived?.Invoke(handshakePacketView);
            return;
        }

        if (decryptor is null)
        {
            var exception = new MapleSessionException("Could not initialize vectors.");
            Error?.Invoke(exception);

            return;
        }

        if (packet.RequiresContinuation)
        {
            _stream.SaveForContinuation(packet.Id, packet.Data);
        }

        Decrypt(ref packet);
        PacketDecrypted?.Invoke(new MapleSessionViewEventArgs(args, packet));

        if (packet.Leftovers.Length == 0) return;

        int newOffset = packet.ParentReaded + packet.Header.Length + packet.Payload.Length - packet.ContinuationLength;
        ProcessPayload(args, packet.Leftovers, packet.Id, newOffset);
    }

    public void ProcessDecrypted(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        if (!_stream.IsFragment(packet))
        {
            EncryptAndSend(args);
            return;
        }

        var outBuffer = _stream.GetOrCreateBuffer(packet.Id, packet.TotalLength - packet.ContinuationLength, packet.IsIncoming);

        Encrypt(ref packet);

        packet.Data[packet.ContinuationLength..].CopyTo(outBuffer.AsSpan(packet.ParentReaded));

        if (packet.Leftovers.Length == 0)
        {
            var finalData = _stream.Finalize(packet.Id, packet.IsIncoming);

            if (finalData is null) return;

            ReplaceAndAsend(finalData.Value.AsSpan(), args);

            _stream.CleanPayload(packet.Id);
        }
    }

    private void EncryptAndSend(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        Encrypt(ref packet);
        ReplaceAndAsend(packet.Data[packet.ContinuationLength..], args);
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
