using System.CommandLine;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Unlimitedinf.Utilities;

namespace Unlimitedinf.Tom.Commands
{
    internal static class WebSocketServerCommand
    {
        public static Command Create()
        {
            Option<DirectoryInfo> workingDirOption = new Option<DirectoryInfo>("--working-dir")
            {
                Description = "The working directory to be used for serving files. Only files that are a children of this directory can be copied.",
                DefaultValueFactory = _ => new(Environment.CurrentDirectory),
            }.AcceptExistingOnly();
            Option<double> mbpsLimitOption = new("--mbps")
            {
                Description = "Rate-limit the sending and receiving of files to this megabits per second.",
                DefaultValueFactory = _ => 0,
            };
            Option<string> hostOption = new("--host")
            {
                Description = "The hostname to run on. By default accepts any hostname.",
                DefaultValueFactory = _ => "+",
            };
            Option<int> portOption = new("--port", "-p")
            {
                Description = "The port to listen on for connections.",
                DefaultValueFactory = _ => Random.Shared.Next(49152, 65536),
            };
            Option<FileInfo> httpsPfxPathOption = new Option<FileInfo>("--https-pfx-path")
            {
                Description = "If provided, will use this certificate for HTTPS instead of the built-in ASP.NET Core certificate.",
            }.AcceptLegalFilePathsOnly();
            Option<FileInfo> httpsCrtPemPathOption = new Option<FileInfo>("--https-crtpem-path")
            {
                Description = "If provided, will use this certificate for HTTPS instead of the built-in ASP.NET Core certificate. Needs to be paired with --https-keypem-path to work.",
            }.AcceptLegalFilePathsOnly();
            Option<FileInfo> httpsKeyPemPathOption = new Option<FileInfo>("--https-keypem-path")
            {
                Description = "If provided, will use this certificate for HTTPS instead of the built-in ASP.NET Core certificate. Needs to be paired with --https-crtpem-path to work.",
            }.AcceptLegalFilePathsOnly();
            Option<bool> httpsGenerateCertOption = new("--https-generate-cert")
            {
                Description = "If provided, will generate an https certificate with a nonsense subject name to be used for manual validation.",
            };
            Command command = new("wss", "Creates a web socket server to be used for p2p communication.")
            {
                workingDirOption,
                mbpsLimitOption,
                hostOption,
                portOption,
                httpsPfxPathOption,
                httpsCrtPemPathOption,
                httpsKeyPemPathOption,
                httpsGenerateCertOption,
            };
            command.SetAction(parseResult =>
            {
                DirectoryInfo workingDir = parseResult.GetRequiredValue(workingDirOption);
                double mbpsLimit = parseResult.GetRequiredValue(mbpsLimitOption);
                string host = parseResult.GetRequiredValue(hostOption);
                int port = parseResult.GetRequiredValue(portOption);
                FileInfo httpsPfxPath = parseResult.GetValue(httpsPfxPathOption);
                FileInfo httpsCertPemPath = parseResult.GetValue(httpsCrtPemPathOption);
                FileInfo httpsKeyPemPath = parseResult.GetValue(httpsKeyPemPathOption);
                bool httpsGenerateCert = parseResult.GetValue(httpsGenerateCertOption);
                return HandleAsync(workingDir, mbpsLimit, host, port, httpsPfxPath, httpsCertPemPath, httpsKeyPemPath, httpsGenerateCert);
            });
            return command;
        }

        private static Task HandleAsync(DirectoryInfo workingDir, double mbpsLimit, string host, int port, FileInfo httpsPfxPath, FileInfo httpsCertPemPath, FileInfo httpsKeyPemPath, bool httpsGenerateCert)
        {
            X509Certificate2 httpsCert = default;
            if (httpsPfxPath != default)
            {
#if NET10_0
                httpsCert = X509CertificateLoader.LoadPkcs12FromFile(httpsPfxPath.FullName, password: default);
#else
                httpsCert = new X509Certificate2(httpsPfxPath.FullName);
#endif
            }
            else if (httpsCertPemPath != default && httpsKeyPemPath != default)
            {
                httpsCert = X509Certificate2.CreateFromPemFile(httpsCertPemPath.FullName, httpsKeyPemPath.FullName);
            }
            else if (httpsGenerateCert)
            {
                string subjectName = $"CN={Rando.GetString(Rando.RandomType.Name).Replace('_', '-')}.{port}.tomwssself";
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
