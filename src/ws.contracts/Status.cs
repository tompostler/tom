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
        public double GigabytesReceived => Math.Round(this.BinaryBytesReceived / 1_000_000_000d, 2);
        public double GibibytesReceived => Math.Round(this.BinaryBytesReceived / 1_073_741_824d, 2);

        internal ulong binaryBytesSent;
        public ulong BinaryBytesSent { get => this.binaryBytesSent; set => this.binaryBytesSent = value; }
        public double GigabytesSent => Math.Round( this.BinaryBytesSent / 1_000_000_000d,2);
        public double GibibytesSent => Math.Round(this.BinaryBytesSent / 1_073_741_824d, 2);

        internal ulong textMessagesReceived;
        public ulong TextMessagesReceived { get => this.textMessagesReceived; set => this.textMessagesReceived = value; }

        internal ulong textMessagesSent;
        public ulong TextMessagesSent { get => this.textMessagesSent; set => this.textMessagesSent = value; }
    }
}
