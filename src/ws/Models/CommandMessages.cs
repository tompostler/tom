using System;
using System.Collections.Generic;

namespace Unlimitedinf.Tom.WebSocket.Models
{
    public enum CommandType
    {
        error,
        motd,
        cd,
        ls,
        get,
        put,
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

    public sealed class TrimmedFileSystemObjectInfo
    {
        public string Name { get; set; }
        public DateTime Modified { get; set; }

        public string Type { get; set; }

        // Only applicable to files
        public long Length { get; set; }
    }

    public sealed class CommandMessageLsResponse : CommandMessage
    {
        public new CommandType Type => CommandType.ls;

        public string CurrentDirectory { get; set; }
        public List<TrimmedFileSystemObjectInfo> Dirs { get; set; }
        public List<TrimmedFileSystemObjectInfo> Files { get; set; }
    }

    public sealed class CommandMessageGetRequest : CommandMessage
    {
        public new CommandType Type => CommandType.get;

        public string Target { get; set; }
    }

    public sealed class CommandMessageGetResponse : CommandMessage
    {
        public new CommandType Type => CommandType.get;

        public List<TrimmedFileSystemObjectInfo> Files { get; set; }
    }

    public sealed class CommandMessageGetEndResponse : CommandMessage
    {
        public new CommandType Type => CommandType.get;

        public string HashSHA256 { get; set; }
    }

    public sealed class CommandMessagePutRequest : CommandMessage
    {
        public new CommandType Type => CommandType.put;

        public List<TrimmedFileSystemObjectInfo> Files { get; set; }
    }

    public sealed class CommandMessagePutEndRequest : CommandMessage
    {
        public new CommandType Type => CommandType.put;

        public string HashSHA256 { get; set; }
    }
}
