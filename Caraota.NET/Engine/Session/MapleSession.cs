using Caraota.Crypto;
using Caraota.Crypto.Packets;
using Caraota.NET.Common.Events;
using Caraota.NET.Engine.Logic;
using Caraota.NET.Infrastructure.Interception;
using Caraota.NET.Infrastructure.TCP;

namespace Caraota.NET.Engine.Session;

public sealed class MapleSession(IWinDivertSender winDivertSender)
{
    public event MaplePacketEventDelegate? PacketDecrypted;
    public delegate void MaplePacketEventDelegate(MapleSessionPacket args);

    private MaplePacketProcessor? _packetProcessor;

    private readonly IWinDivertSender _winDivertSender = winDivertSender;
    private readonly MapleSessionInitializer _sessionInitializer = new(winDivertSender);
    private readonly PacketReassembler _reassembler = new();

    public HandshakeSessionPacket Initialize(WinDivertPacketEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
    {
        var handshake = _sessionInitializer.Initialize(winDivertPacket, payload);

        _packetProcessor = new(_sessionInitializer);

        return handshake;
    }

    public bool IsInitialized()
        => _sessionInitializer.SessionSuccess;

    public void Reset()
        => _sessionInitializer.SessionSuccess = false;

    public void Decrypt(WinDivertPacketEventArgs args, ReadOnlySpan<byte> payload, long? parentId = null, int? parentReaded = null)
    {
        var decryptor = _sessionInitializer.GetDecryptor(args.IsIncoming)!;
        var packet = PacketFactory.Parse(payload, decryptor.IV.Span, args.IsIncoming, parentId, parentReaded);

        _packetProcessor!.Decrypt(ref packet);

        PacketDecrypted?.Invoke(new MapleSessionPacket(args, packet));

        DecryptLeftover(args, packet);
    }

    public void EncryptAndSend(MapleSessionPacket args)
    {
        var packet = args.DecodedPacket;
        _packetProcessor!.Encrypt(ref packet);

        var original = args.WinDivertPacket;
        var data = packet.Data;
        var address = args.Address;
        var isIncoming = packet.IsIncoming;

        _winDivertSender.ReplaceAndSend(original, data, address, isIncoming);
    }

    public void DecryptLeftover(WinDivertPacketEventArgs args, DecodedPacket parentPacket)
    {
        if (parentPacket.Leftovers.IsEmpty) return;

        int newOffset = parentPacket.ParentReaded + parentPacket.Header.Length + parentPacket.Payload.Length;

        Decrypt(args, parentPacket.Leftovers, parentPacket.Id, newOffset);
    }

    public bool ProcessLeftovers(MapleSessionPacket args)
    {
        var packet = args.DecodedPacket;

        if (!_reassembler.IsFragment(packet.Id, packet.Leftovers.Length, packet.IsIncoming))
        {
            return false;
        }

        byte[] outBuffer = _reassembler.GetOrCreateBuffer(packet.Id, packet.TotalLength, packet.IsIncoming);

        if (_packetProcessor!.Validate(packet))
        {
            _packetProcessor.Encrypt(ref packet);
            packet.Data.CopyTo(outBuffer.AsSpan().Slice(packet.ParentReaded, packet.Data.Length));
        }
        else
        {
            packet.Header.CopyTo(outBuffer.AsSpan().Slice(packet.ParentReaded, 4));
            packet.Payload.CopyTo(outBuffer.AsSpan().Slice(packet.ParentReaded + 4, packet.Payload.Length));
        }

        if (packet.Leftovers.Length == 0)
        {
            byte[]? finalData = _reassembler.Finalize(packet.Id, packet.IsIncoming);
            if (finalData != null)
            {
                _winDivertSender.ReplaceAndSend(args.WinDivertPacket, finalData.AsSpan(), args.Address, packet.IsIncoming);
            }
        }

        return true;
    }
}
