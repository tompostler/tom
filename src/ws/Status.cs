using System;

namespace Unlimitedinf.Tom.WebSocket
{
    public sealed class Status
    {
        internal static Status Instance { get; } = new();

        public DateTime StartTime { get; set; } = DateTime.Now;
        public TimeSpan UpTime => DateTime.Now.Subtract(this.StartTime);

        public ulong BinaryBytesReceived;
        public double GigabytesReceived => this.BinaryBytesReceived / 1_000_000_000d;

        public ulong BinaryBytesSent;
        public double GigabytesSent => this.BinaryBytesSent / 1_000_000_000d;

        internal ulong textMessagesReceived;
        public ulong TextMessagesReceived { get => this.textMessagesReceived; set => this.textMessagesReceived = value; }

        internal ulong textMessagesSent;
        public ulong TextMessagesSent { get => this.textMessagesSent; set => this.textMessagesSent = value; }
    }
}
