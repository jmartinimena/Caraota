using System;
using System.Collections.Generic;
using System.Text;

namespace Caraota.NET.Common.Attributes
{
    public class PacketHandlerAttribute(ushort opcode) : Attribute
    {
        public ushort Opcode => opcode;
    }
}
