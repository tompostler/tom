namespace Unlimitedinf.Tom.WebSocket.Models
{
    public enum CommandType
    {
        unknown,
        motd,
        cd,
        ls
    }

    public abstract class CommandMessageBase
    {
        public CommandType Type { get; set; }
    }

    public sealed class CommandMessageUnknown : CommandMessageBase
    {
        public new CommandType Type => CommandType.unknown;

        public string Payload { get; set; }
    }

    public sealed class CommandMessageMotdRequest : CommandMessageBase
    {
        public new CommandType Type => CommandType.motd;
    }

    public sealed class CommandMessageMotdResponse : CommandMessageBase
    {
        public new CommandType Type => CommandType.motd;

        public Status Status { get; set; }
        public long BytesPerSecondLimit { get; set; }
        public string CurrentDirectory { get; set; }
    }

    public sealed class CommandMessageCd : CommandMessageBase
    {
        public new CommandType Type => CommandType.cd;

        public string Target { get; set; }
    }
}
