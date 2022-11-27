namespace Unlimitedinf.Tom.WebSocket.Models
{
    public enum CommandType
    {
        unknown,
        motd,
        cd,
        ls
    }

    public class CommandMessage
    {
        public CommandType Type { get; set; }
    }

    public sealed class CommandMessageUnknownResponse : CommandMessage
    {
        public new CommandType Type => CommandType.unknown;

        public string Payload { get; set; }
    }

    public sealed class CommandMessageMotdRequest : CommandMessage
    {
        public new CommandType Type => CommandType.motd;
    }

    public sealed class CommandMessageMotdResponse : CommandMessage
    {
        public new CommandType Type => CommandType.motd;

        public Status Status { get; set; }
        public long BytesPerSecondLimit { get; set; }
        public string CurrentDirectory { get; set; }
    }

    public sealed class CommandMessageCd : CommandMessage
    {
        public new CommandType Type => CommandType.cd;

        public string Target { get; set; }
    }
}
