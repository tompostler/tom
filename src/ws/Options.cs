using System;
using System.Security.Cryptography.X509Certificates;

namespace Unlimitedinf.Tom.WebSocket
{
    public sealed class Options
    {
        public long BytesPerSecondLimit { get; set; } = 1_000_000;
        public string Host { get; set; } = "+";
        public int Port { get; set; } = Random.Shared.Next(49152, 65536);
        public X509Certificate2 CustomHttpsCertificate { get; set; }
        public string Token { get; internal set; }
    }
}
