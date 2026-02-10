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

    // Hay un caso de fragmentacion de paquetes
    private int startLen = 0;
    private byte[] startBuffer = new byte[4096];
    private byte[] payloadBuffer = new byte[32000];
    public void ProcessPacket(WinDivertPacketViewEventArgs args, Span<byte> payload, long? parentId = null, int? parentReaded = null)
    {
        // Si tenemos algo guardado lo ponemos al comienzo de nuestro payload y continuamos
        if (startLen > 0)
        {
            startBuffer.AsSpan(0, startLen).CopyTo(payloadBuffer);
            payload.CopyTo(payloadBuffer.AsSpan(startLen));
            payload = payloadBuffer.AsSpan(0, startLen + payload.Length);
        }

        var decryptor = _sessionManager.GetDecryptor(args.IsIncoming);
        var packet = PacketFactory.Parse(payload, decryptor.IV.Span, args.IsIncoming, parentId, parentReaded);

        // Si fue reconstruido establecemos la bandera para el invoke
        if(startLen > 0)
        {
            packet.Rebuilt = true;
            startLen = 0;
        }

        // Mandamos el comienzo para reconstruirse con el siguiente paquete, el dispatcher lo ignorara
        // solo pasara por el dispatcher cuando el paquete este completo
        if (packet.RequiresContinuation)
        {
            packet.Data.CopyTo(startBuffer);
            startLen = packet.Data.Length;
        }

        decryptor.Decrypt(ref packet);
        PacketDecrypted?.Invoke(new MapleSessionViewEventArgs(args, packet));

        DecryptLeftover(args, packet);
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
