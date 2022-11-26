using System;
using System.CommandLine;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.Commands
{
    internal static class WebSocketServerCommand
    {
        public static Command Create()
        {
            Command command = new("wss", "Creates a web socket server to be used for p2p communication.");

            Option<DirectoryInfo> workingDirOpt = new(
                "--working-dir",
                () => new(Environment.CurrentDirectory),
                "The working directory to be used for serving files. Only files that are a children of this directory can be copied.");
            command.AddOption(workingDirOpt);

            Option<double> mbpsLimitOpt = new(
                "--mbps",
                () => 0,
                "Rate-limit the sending and receiving of files to this megabits per second.");
            command.AddOption(mbpsLimitOpt);

            Option<string> hostOpt = new(
                "--host",
                () => "+",
                "The hostname to run on. By default accepts any hostname.");
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

            Option<bool> httpsGenerateCertOpt = new(
                "--https-generate-cert",
                "If provided, will generate an https certificate with a nonsense subject name to be used for manual validation.");
            command.AddOption(httpsGenerateCertOpt);

            command.SetHandler(HandleAsync, workingDirOpt, mbpsLimitOpt, hostOpt, portOpt, httpsPfxPathOpt, httpsCertPemPathOpt, httpsKeyPemPathOpt, httpsGenerateCertOpt);
            return command;
        }

        private static Task HandleAsync(DirectoryInfo workingDir, double mbpsLimit, string host, int port, FileInfo httpsPfxPath, FileInfo httpsCertPemPath, FileInfo httpsKeyPemPath, bool httpsGenerateCert)
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
            else if (httpsGenerateCert)
            {
                string subjectName = $"CN={RandomCommand.InnerHandle(RandomCommand.RandomType.Name).Replace('_','-')}.{port}.tomwssself";
                httpsCert = new CertificateRequest(subjectName, ECDsa.Create(), HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7));
            }

            Environment.CurrentDirectory = workingDir.FullName;

            return WebSocket.Program.MainAsync(
                new()
                {
                    BytesPerSecondLimit = (long)(mbpsLimit * 1_000_000 / 8),
                    Host = host,
                    Port = port,
                    CustomHttpsCertificate = httpsCert,
                });
        }
    }
}
