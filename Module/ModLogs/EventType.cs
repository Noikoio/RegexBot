using System;

namespace Noikoio.RegexBot.Module.ModLogs
{
    // Types of non-custom events that can be referenced by ModLogs in configuration.
    // Enum value names will show themselves to the user in the form of strings valid in configuration,
    // so try not to change those without good reason.
    [Flags]
    enum EventType
    {
        None        = 0x0,
        Note        = 0x1,
        Warn        = 0x2,
        Kick        = 0x4,
        Ban         = 0x8,
        JoinGuild   = 0x10,
        LeaveGuild  = 0x20,
        NameChange  = 0x40,
        MsgEdit     = 0x80,
        MsgDelete   = 0x100
    }
}
