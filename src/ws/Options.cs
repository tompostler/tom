using System.Security.Cryptography.X509Certificates;
using System;

namespace Unlimitedinf.Tom.WebSocket
{
    public sealed class Options
    {
        public string Host { get; set; } = "+";
        public int Port { get; set; } = Random.Shared.Next(49152, 65536);
        public X509Certificate2 HttpsCertificate { get; set; }
        public string Password { get; internal set; }
    }
}
