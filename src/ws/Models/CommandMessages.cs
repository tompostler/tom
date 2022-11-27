using System;
using System.Collections.Generic;

namespace Unlimitedinf.Tom.WebSocket.Models
{
    public enum CommandType
    {
        error,
        motd,
        cd,
        ls
    }

    public class CommandMessage
    {
        public CommandType Type { get; set; }
    }

    public sealed class CommandMessageErrorResponse : CommandMessage
    {
        public new CommandType Type => CommandType.error;

        public string Payload { get; set; }
    }

    public sealed class CommandMessageMotdRequest : CommandMessage
    {
        public new CommandType Type => CommandType.motd;
    }

    public sealed class CommandMessageMotdResponse : CommandMessage
    {
        public new CommandType Type => CommandType.motd;

        public string Message { get; set; }
        public Status Status { get; set; }
        public double MegabitPerSecondLimit { get; set; }
        public string CurrentDirectory { get; set; }
    }

    public sealed class CommandMessageCd : CommandMessage
    {
        public new CommandType Type => CommandType.cd;

        public string Target { get; set; }
    }

    public sealed class CommandMessageLsRequest : CommandMessage
    {
        public new CommandType Type => CommandType.ls;
    }

    public sealed class CommandMessageLsResponse : CommandMessage
    {
        public new CommandType Type => CommandType.ls;

        public string CurrentDirectory { get; set; }
        public List<TrimmedFileSystemObjectInfo> Dirs { get; set; }
        public List<TrimmedFileSystemObjectInfo> Files { get; set; }

        public sealed class TrimmedFileSystemObjectInfo
        {
            public string Name { get; set; }
            public DateTime Modified { get; set; }

            // Only applicable to files
            public long? Length { get; set; }
        }
    }
}
