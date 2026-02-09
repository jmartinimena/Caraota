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

    private readonly IWinDivertSender _winDivertSender = winDivertSender;
    private readonly IPacketReassembler _reassembler = new PacketReassembler();
    private readonly MapleSessionManager _sessionManager = new(winDivertSender);

    public bool Success => _sessionManager.Success;

    public HandshakeSessionPacket Initialize(WinDivertPacketViewEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
        => _sessionManager.Initialize(winDivertPacket, payload);

    private Memory<byte> inStart = Array.Empty<byte>();
    private Memory<byte> outStart = Array.Empty<byte>();
    public void ProcessPacket(WinDivertPacketViewEventArgs args, Span<byte> payload, long? parentId = null, int? parentReaded = null)
    {
        var start = args.IsIncoming ? inStart : outStart;

        // Si no tenemos suficiente para un header guardamos y salimos
        if(start.Length == 0 && payload.Length < 4)
        {
            start = new byte[payload.Length];
            payload.CopyTo(start.Span);
            return;
        }

        // Si tenemos algo guardado lo ponemos al comienzo de nuestro payload y continuamos
        if(start.Length > 0)
        {
            Span<byte> newPayload = new byte[start.Length + payload.Length];
            start.Span.CopyTo(newPayload);
            payload.CopyTo(newPayload[start.Length..]);

            payload = newPayload;

            start = Array.Empty<byte>();
        }

        var decryptor = _sessionManager.GetDecryptor(args.IsIncoming);
        var packet = PacketFactory.Parse(payload, decryptor.IV.Span, args.IsIncoming, parentId, parentReaded);

        // Si el length del header reporta un tamano mayor del que nos viene en el payload, requiere continuacion
        if (packet.RequiresContinuation)
        {
            start = new byte[packet.Data.Length];
            packet.Data.CopyTo(start.Span);
        }
        else // Si no es el caso continuamos el flujo normal
        {
            decryptor.Decrypt(ref packet);
            PacketDecrypted?.Invoke(new MapleSessionViewEventArgs(args, packet));
            DecryptLeftover(args, packet);
        }
    }

    public void EncryptAndSend(MapleSessionViewEventArgs args)
    {
        var decryptor = _sessionManager.GetEncryptor(args.MaplePacketView.IsIncoming);
        var packet = args.MaplePacketView;
        decryptor.Encrypt(ref packet);

        var original = args.WinDivertPacket;
        var data = packet.Data;
        var address = args.Address;
        _winDivertSender.ReplaceAndSend(original, data, address);
    }

    public bool ProcessLeftovers(MapleSessionViewEventArgs args)
    {
        var packet = args.MaplePacketView;

        if (!_reassembler.IsFragment(packet))
            return false;

        byte[] outBuffer = _reassembler.GetOrCreateBuffer(packet.Id, packet.TotalLength, packet.IsIncoming);

        var decryptor = _sessionManager.GetEncryptor(args.MaplePacketView.IsIncoming);
        decryptor.Encrypt(ref packet);
        packet.Data.CopyTo(outBuffer.AsSpan(packet.ParentReaded));

        if (packet.Leftovers.Length == 0)
        {
            byte[]? finalData = _reassembler.Finalize(packet.Id, packet.IsIncoming);

            if (finalData != null)
                _winDivertSender.ReplaceAndSend(args.WinDivertPacket, finalData.AsSpan(), args.Address);
        }

        return true;
    }

    private void DecryptLeftover(WinDivertPacketViewEventArgs args, MaplePacketView parentPacket)
    {
        if (parentPacket.Leftovers.IsEmpty) return;

        int newOffset = parentPacket.ParentReaded + parentPacket.Header.Length + parentPacket.Payload.Length;
        ProcessPacket(args, parentPacket.Leftovers, parentPacket.Id, newOffset);
    }
}
