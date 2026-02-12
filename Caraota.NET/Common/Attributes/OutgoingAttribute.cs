namespace Caraota.NET.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OutgoingAttribute(ushort opcode) : PacketHandlerAttribute(opcode)
    {
    }
}
