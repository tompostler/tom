using System;

namespace Unlimitedinf.Tom.WebSocket
{
    public sealed class Status
    {
        public DateTime StartTime { get; } = DateTime.Now;
        public TimeSpan UpTime => DateTime.Now.Subtract(this.StartTime);

        public ulong BytesReceived;
        public double MegabytesReceived => this.BytesReceived / 1_000_000d;
        public double GigabytesReceived => this.BytesReceived / 1_000_000_000d;

        public ulong BytesSent;
        public double MegabytesSent => this.BytesSent / 1_000_000d;
        public double GigabytesSent => this.BytesSent / 1_000_000_000d;
    }
}
