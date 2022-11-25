using System;
using System.CommandLine;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.Commands
{
    internal static class WebSocketServer
    {
        public static Command Create()
        {
            Command command = new("wss", "Creates a web socket server to be used for p2p communication.");

            Option<string> hostOpt = new(
                "--host",
                () => "+",
                "The hostname to run on. By default accepts any hostname.");
            command.AddAlias("-h");
            command.AddOption(hostOpt);

            Option<int> portOpt = new(
                "--port",
                () => Random.Shared.Next(49152, 65536),
                "The port to listen on for connections.");
            portOpt.AddAlias("-p");
            command.AddOption(portOpt);

            Option<FileInfo> httpsPfxPathOpt = new(
                "--https-pfx-path",
                "If provided, will use this certificate for HTTPS instead of the built-in ASP.NET Core certificate.");
            command.AddOption(httpsPfxPathOpt);

            Option<FileInfo> httpsCertPemPathOpt = new(
                "--https-certpem-path",
                "If provided, will use this certificate for HTTPS instead of the built-in ASP.NET Core certificate. Needs to be paired with --https-keypem-path to work.");
            command.AddOption(httpsCertPemPathOpt);

            Option<FileInfo> httpsKeyPemPathOpt = new(
                "--https-keypem-path",
                "If provided, will use this certificate for HTTPS instead of the built-in ASP.NET Core certificate. Needs to be paired with --https-certpem-path to work.");
            command.AddOption(httpsKeyPemPathOpt);

            command.SetHandler(HandleAsync, hostOpt, portOpt, httpsPfxPathOpt, httpsCertPemPathOpt, httpsKeyPemPathOpt);
            return command;
        }

        private static Task HandleAsync(string host, int port, FileInfo httpsPfxPath, FileInfo httpsCertPemPath, FileInfo httpsKeyPemPath)
        {
            X509Certificate2 httpsCert = default;
            if (httpsPfxPath != default)
            {
                httpsCert = new X509Certificate2(httpsPfxPath.FullName);
            }
            else if (httpsCertPemPath != default && httpsKeyPemPath != default)
            {
                httpsCert = X509Certificate2.CreateFromPemFile(httpsCertPemPath.FullName, httpsKeyPemPath.FullName);
            }

            return WebSocket.Program.MainAsync(
                new()
                {
                    Host = host,
                    Port = port,
                    HttpsCertificate = httpsCert,
                });
        }
    }
}
