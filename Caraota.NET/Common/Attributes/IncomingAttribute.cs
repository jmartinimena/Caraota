namespace Caraota.NET.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IncomingAttribute(ushort opcode) : PacketHandlerAttribute(opcode)
    {
    }
}
