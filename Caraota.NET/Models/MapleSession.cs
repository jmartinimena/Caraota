using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Caraota.Crypto;
using Caraota.Crypto.Packets;
using Caraota.Crypto.Processing;
using Caraota.NET.Events;

namespace Caraota.NET.Models;

public sealed class MapleSession
{
    private const int MAX_VERSION = 256;
    private const int HANDSHAKE_V82_LENGTH = 16;
    private const int HANDSHAKE_V62_LENGTH = 15;
    private const int VERSION_OFFSET = 2;
    private const int VERSION_SIZE = sizeof(ushort);

    private MapleCrypto? _serverRecv;
    private MapleCrypto? _serverSend;
    private MapleCrypto? _clientRecv;
    private MapleCrypto? _clientSend;

    public bool SessionSuccess { get; set; }

    public MapleCrypto? ServerRecv => _serverRecv;
    public MapleCrypto? ServerSend => _serverSend;
    public MapleCrypto? ClientRecv => _clientRecv;
    public MapleCrypto? ClientSend => _clientSend;

    public event MaplePacketEventDelegate? OnOutgoingPacket;
    public event MaplePacketEventDelegate? OnIncomingPacket;
    public event EventHandler<HandshakePacketEventArgs>? OnHandshake;

    public delegate void MaplePacketEventDelegate(MapleSessionEventArgs args);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InitSession(WinDivertPacketEventArgs winDivertPacket, ReadOnlySpan<byte> payload)
    {
        if (TryGetVersion(payload, out ushort version))
        {
            CreateCryptoInstances(winDivertPacket, payload, version);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool TryGetVersion(ReadOnlySpan<byte> data, out ushort version)
    {
        version = 0;

        switch (data.Length)
        {
            case HANDSHAKE_V82_LENGTH:
            case HANDSHAKE_V62_LENGTH:
                if (data.Length >= VERSION_OFFSET + VERSION_SIZE)
                {
                    version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(VERSION_OFFSET, VERSION_SIZE));
                    return version <= MAX_VERSION;
                }
                break;
        }

        return false;
    }

    private void CreateCryptoInstances(WinDivertPacketEventArgs winDivertPacket, ReadOnlySpan<byte> payload, ushort version)
    {
        var handshakePacket = new HandshakePacketEventArgs(new MapleSessionEventArgs(winDivertPacket, default), payload);

        try
        {
            if (SessionSuccess) return;

            _serverRecv = new MapleCrypto(handshakePacket.RIV, version);
            _serverSend = new MapleCrypto(handshakePacket.RIV, version);
            _clientRecv = new MapleCrypto(handshakePacket.SIV, version);
            _clientSend = new MapleCrypto(handshakePacket.SIV, version);

            SessionSuccess = true;

            OnHandshake?.Invoke(this, handshakePacket);
        }
        catch (Exception ex)
        {
            SessionSuccess = false;
            throw new MapleSessionException("Failed to initialize crypto session", ex);
        }
    }

    public void DecryptPacket(WinDivertPacketEventArgs args, DecodedPacket packet, bool isIncoming)
    {
        EnsureSessionInitialized();

        var session = GetSessionForDirection(isIncoming);
        if (session is null)
        {
            throw new InvalidOperationException($"Session for {(isIncoming ? "incoming" : "outgoing")} not initialized");
        }

        DecryptPacketRecursive(args, packet, isIncoming, session);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MapleCrypto? GetSessionForDirection(bool isIncoming) =>
        isIncoming ? _serverRecv : _clientRecv;

    private void DecryptPacketRecursive(WinDivertPacketEventArgs args, DecodedPacket packet, bool isIncoming, MapleCrypto session)
    {
        if (session.Validate(packet, isIncoming))
        {
            session.Decrypt(ref packet);
        }

        var packetArgs = new MapleSessionEventArgs(args, packet);
        RaisePacketEvent(isIncoming, packetArgs);

        var leftovers = packet.Leftovers;
        if (!leftovers.IsEmpty)
        {
            DecryptPacketLeftovers(args, packet, leftovers, isIncoming, session);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaisePacketEvent(bool isIncoming, MapleSessionEventArgs args)
    {
        if (isIncoming)
        {
            OnIncomingPacket?.Invoke(args);
        }
        else
        {
            OnOutgoingPacket?.Invoke(args);
        }
    }

    private void DecryptPacketLeftovers(WinDivertPacketEventArgs args, DecodedPacket parentPacket,
        ReadOnlySpan<byte> leftovers, bool isIncoming, MapleCrypto session)
    {
        var iv = session.IV.Span;
        int newOffset = parentPacket.ParentReaded + parentPacket.Header.Length + parentPacket.Payload.Length;

        var childPacket = PacketFactory.Parse(
            leftovers,
            iv,
            isIncoming,
            parentPacket.Id,
            newOffset
        );

        DecryptPacketRecursive(args, childPacket, isIncoming, session);
    }

    [MemberNotNull(nameof(_serverRecv), nameof(_serverSend), nameof(_clientRecv), nameof(_clientSend))]
    private void EnsureSessionInitialized()
    {
        if (!SessionSuccess || _serverRecv is null || _clientRecv is null)
        {
            throw new InvalidOperationException("Session not initialized. Call InitSession first.");
        }
    }
}

public class MapleSessionException : Exception
{
    public MapleSessionException(string message) : base(message) { }
    public MapleSessionException(string message, Exception innerException)
        : base(message, innerException) { }
}