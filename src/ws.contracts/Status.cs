using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unlimitedinf.Tom.WebSocket")]

namespace Unlimitedinf.Tom.WebSocket.Contracts
{
    public sealed class Status
    {
        internal static Status Instance { get; } = new();

        public DateTime StartTime { get; set; } = DateTime.Now;
        public TimeSpan UpTime => DateTime.Now.Subtract(this.StartTime);

        internal ulong binaryBytesReceived;
        public ulong BinaryBytesReceived { get => this.binaryBytesReceived; set => this.binaryBytesReceived = value; }
        public double GigabytesReceived => this.BinaryBytesReceived / 1_000_000_000d;

        internal ulong binaryBytesSent;
        public ulong BinaryBytesSent { get => this.binaryBytesSent; set => this.binaryBytesSent = value; }
        public double GigabytesSent => this.BinaryBytesSent / 1_000_000_000d;

        internal ulong textMessagesReceived;
        public ulong TextMessagesReceived { get => this.textMessagesReceived; set => this.textMessagesReceived = value; }

        internal ulong textMessagesSent;
        public ulong TextMessagesSent { get => this.textMessagesSent; set => this.textMessagesSent = value; }
    }
}
