using Caraota.NET.Common.Events;
using Caraota.NET.Infrastructure.Interception;

using MapleInterceptor interceptor = new();

interceptor.ErrorOcurred += OnException;
interceptor.HandshakeReceived += OnHandshake;
interceptor.SessionMonitor.Disconnected += OnDisconnected;

interceptor.PacketDispatcher.OutgoingReceived += OnOutgoingReceived;
interceptor.PacketDispatcher.IncomingReceived += OnIncomingReceived;

interceptor.StartListening(port: 7575);

StartMessage();

void StartMessage()
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine("Waiting for handshake...");
    Console.WriteLine(new string('=', 32));
    Console.ResetColor();
    Console.ReadLine();
}

Task OnException(Exception e)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR]");
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {e.Message}");
    if (e.InnerException is not null)
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {e.InnerException.Message}");
    Console.WriteLine(new string('=', 32));
    Console.ResetColor();

    return Task.CompletedTask;
}

Task OnHandshake(HandshakeEventArgs args)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[{args.FormattedTime}]");
    Console.WriteLine($"Handshake -> Version {args.Version}, RIV {Convert.ToHexString(args.RIV.ToArray())}, SIV {Convert.ToHexString(args.SIV.ToArray())}, Locale {args.Locale}");
    Console.WriteLine($"Incoming[{args.Opcode}] -> {Convert.ToHexString(args.Payload.ToArray())}");
    Console.WriteLine(new string('=', 32));
    Console.ResetColor();

    return Task.CompletedTask;
}

Task OnDisconnected()
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Desconectado...");
    Console.WriteLine(new string('=', 32));
    Console.ResetColor();

    StartMessage();

    return Task.CompletedTask;
}

Task OnOutgoingReceived(MaplePacketEventArgs args)
{
    if (args.Hijacked)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[Hijacked]");
    }

    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[{args.Packet.FormattedTime}({args.Packet.IVStr})]");
    Console.WriteLine($"Outgoing[{args.Packet.Opcode}] -> {args.Packet.Predict()}");
    //Console.WriteLine($"Outgoing[{args.Packet.Opcode}] -> {args.Packet.ToHexString()}");
    Console.WriteLine(new string('=', 32));
    Console.ResetColor();

    //if (args.Packet.Opcode == 41)
    //{
    //    742A602A290001010000000000110104994494006C003500
    //    var packet = PacketFactory.Create(Convert.FromHexString(""));
    //    interceptor.HijackManager.HijackOnServer();
    //}

    return Task.CompletedTask;
}

Task OnIncomingReceived(MaplePacketEventArgs args)
{
    if (args.Hijacked)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[Hijacked]");
    }

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"[{args.Packet.FormattedTime}({args.Packet.IVStr})]");
    Console.WriteLine($"Incoming[{args.Packet.Opcode}] -> {args.Packet.Predict()}");
    //Console.WriteLine($"Incoming[{args.Packet.Opcode}] -> {args.Packet.ToHexString()}");
    Console.WriteLine(new string('=', 32));
    Console.ResetColor();

    return Task.CompletedTask;
}
