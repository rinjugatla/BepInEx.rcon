﻿namespace Rcon.Internal
{
    internal enum PacketType : int
    {
        Error = -1,
        MultiPacket = 0,
        Command = 2,
        Login = 3,
    }
}
