using System;
using System.CommandLine;
using System.IO;
using System.Net.Security;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.Commands
{
    internal static class WebSocketClientCommand
    {
        public static Command Create()
        {
            Command command = new("wsc", "Establish a web socket client connection to another instance of 'tom wss' for p2p communication.");

            Argument<Uri> endpointArg = new(
                "endpoint",
                "The hostname and port to establish the websocket connection to. E.g wss://localhost:45678");
            command.AddArgument(endpointArg);

            Option<DirectoryInfo> workingDirOpt = new(
                "--working-dir",
                () => new(Environment.CurrentDirectory),
                "The working directory to be used for serving files. Only files that are a children of this directory can be copied.");
            command.AddOption(workingDirOpt);

            Option<string> httpsCertThumbprintOpt = new(
                "--https-cert-thumbprint",
                "Used in scenarios where the HTTPS certificate is not expected to match the hostname of the endpoint.");
            command.AddOption(httpsCertThumbprintOpt);

            Option<TimeSpan> maxSessionDurationOpt = new(
                "--max-session-duration",
                () => TimeSpan.FromDays(3),
                "After this amount of time, the session will be aborted with the server.");
            command.AddOption(maxSessionDurationOpt);

            command.SetHandler(HandleAsync, endpointArg, workingDirOpt, httpsCertThumbprintOpt, maxSessionDurationOpt);
            return command;
        }

        private static async Task HandleAsync(Uri endpoint, DirectoryInfo workingDir, string httpsCertThumbprint, TimeSpan maxSessionDuration)
        {
            Environment.CurrentDirectory = workingDir.FullName;
            using CancellationTokenSource cts = new(maxSessionDuration);



            ClientWebSocket client = new();

            // For non-endpoint matching or self-signed certificates, validate them manually
            // (self-signed certificates are perfectly valid for TLS as long as you are validating the certificate is the one you expect)
            if (!string.IsNullOrWhiteSpace(httpsCertThumbprint))
            {
                client.Options.RemoteCertificateValidationCallback = (_, certificate, _, sslPolicyErrors) =>
                {
                    // If there are no errors, then the endpoint is completely valid (regardless of us providing a specific thumbprint)
                    if (sslPolicyErrors == SslPolicyErrors.None)
                    {
                        return true;
                    }

                    // Only return true if the thumprint of the certificate matches what we're expecting
                    return string.Equals(httpsCertThumbprint, certificate?.GetCertHashString());
                };
            }

            await client.ConnectAsync(endpoint, cts.Token);
        }
    }
}
