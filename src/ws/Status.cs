using System;
using System.Text.Json.Serialization;

namespace Unlimitedinf.Tom.WebSocket
{
    public sealed class Status
    {
        public DateTime StartTime { get; } = DateTime.Now;
        [JsonIgnore]
        public TimeSpan UpTime => DateTime.Now.Subtract(this.StartTime);

        public ulong BinaryBytesReceived;
        [JsonIgnore]
        public double MegabytesReceived => this.BinaryBytesReceived / 1_000_000d;
        [JsonIgnore]
        public double GigabytesReceived => this.BinaryBytesReceived / 1_000_000_000d;

        public ulong BinaryBytesSent;
        [JsonIgnore]
        public double MegabytesSent => this.BinaryBytesSent / 1_000_000d;
        [JsonIgnore]
        public double GigabytesSent => this.BinaryBytesSent / 1_000_000_000d;

        public ulong TextMessagesReceived;
        public ulong TextMessagesSent;
    }
}
