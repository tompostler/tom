using Microsoft.Net.Http.Headers;
using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Tom.WebSocket.Models;
using Unlimitedinf.Utilities;
using Unlimitedinf.Utilities.Extensions;

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

            Option<string> sslCertSubjectNameOpt = new(
                "--ssl-cert-subject",
                "Used in scenarios where the HTTPS certificate is a valid and trusted SSL certificate but the remote name used to reach the destination won't match the presented certificate.");
            command.AddOption(sslCertSubjectNameOpt);

            Option<string> sslCertThumbprintOpt = new(
                "--ssl-cert-thumbprint",
                "Used in scenarios where the HTTPS certificate is a self-signed certificate and the only validation possible is by thumbprint.");
            command.AddOption(sslCertThumbprintOpt);

            Option<TimeSpan> maxSessionDurationOpt = new(
                "--max-session-duration",
                () => TimeSpan.FromDays(3),
                "After this amount of time, the session will be aborted with the server.");
            command.AddOption(maxSessionDurationOpt);

            command.SetHandler(HandleAsync, endpointArg, workingDirOpt, sslCertSubjectNameOpt, sslCertThumbprintOpt, maxSessionDurationOpt);
            return command;
        }

        private static async Task HandleAsync(Uri endpoint, DirectoryInfo workingDir, string sslCertSubjectName, string sslCertThumbprint, TimeSpan maxSessionDuration)
        {
            Environment.CurrentDirectory = workingDir.FullName;
            using CancellationTokenSource cts = new(maxSessionDuration);
            string token = Input.GetString("Authorization token");
            Console.WriteLine();

            // For non-endpoint matching or self-signed certificates, validate them manually
            // (self-signed certificates are perfectly valid for TLS as long as you are validating the certificate is the one you expect)
            bool serverCertificateValidationCallback(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                // If there are no errors, then the endpoint is completely valid (regardless of us providing a specific thumbprint)
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;
                }

                // If a subject name is provided, then perfom that validation
                if (!string.IsNullOrWhiteSpace(sslCertSubjectName))
                {
                    var errorMessage = new StringBuilder($"Remote certificate error: {sslPolicyErrors}.");

                    // If there are chain errors, then it's an invalid certificate (expired, self-signed, etc)
                    if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
                    {
                        _ = errorMessage.AppendLine($"Chain errors: {string.Join(";", chain?.ChainStatus.Select(c => $"{c.Status}:{c.StatusInformation}"))}");
                        Console.Error.WriteLine(errorMessage.ToString());
                        return false;
                    }

                    // Reject on any other SslPolicyErrors we're not explicitly handling
                    // SslPolicyErrors is a flag enum and ~ is a bitwise inversion. So this basically reads as "if there are any bits set in sslPolicyErrors that are not RemoteCertificateNameMismatch"
                    if ((sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                    {
                        _ = errorMessage.AppendLine($"Unhandled {nameof(SslPolicyErrors)} ({sslPolicyErrors})");
                        Console.Error.WriteLine(errorMessage.ToString());
                        return false;
                    }

                    // This is the only other location where we may process a valid request
                    if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                    {
                        const string cnPrefix = "CN=";
                        string cnComponent = certificate.Subject.Split(',').Single(c => c.StartsWith(cnPrefix)).Substring(cnPrefix.Length).Trim();

                        if (string.Equals(sslCertSubjectName, cnComponent, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                        else
                        {
                            _ = errorMessage.AppendLine($"Subject name [{certificate.Subject}] (parsed: {cnComponent}) was not expected: {sslCertSubjectName}");
                            Console.Error.WriteLine(errorMessage.ToString());
                            return false;
                        }
                    }
                }

                // Only return true if the thumprint of the certificate matches what we're expecting
                return string.Equals(sslCertThumbprint, certificate?.GetCertHashString(), StringComparison.OrdinalIgnoreCase);
            }

            // Set up the basic httpClient
            HttpClient httpClient = new();
            if (!string.IsNullOrWhiteSpace(sslCertSubjectName) || !string.IsNullOrWhiteSpace(sslCertThumbprint))
            {
                HttpClientHandler handler = new()
                {
                    ServerCertificateCustomValidationCallback = (_, certificate, chain, sslPolicyErrors) => serverCertificateValidationCallback(certificate, chain, sslPolicyErrors)
                };
                httpClient = new(handler);
            }
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);

            // Set up the web socket client
            ClientWebSocket wsClient = new();
            if (!string.IsNullOrWhiteSpace(sslCertSubjectName) || !string.IsNullOrWhiteSpace(sslCertThumbprint))
            {
                wsClient.Options.RemoteCertificateValidationCallback = (_, certificate, chain, sslPolicyErrors) => serverCertificateValidationCallback(certificate, chain, sslPolicyErrors);
            }
            wsClient.Options.SetRequestHeader(HeaderNames.Authorization, httpClient.DefaultRequestHeaders.Authorization.ToString());

            // First attempt a basic ping to make sure the routing, authentication, and ssl validation are all correct
            Uri pingEndpoint = endpoint.Scheme.StartsWith("ws") ? new($"http{endpoint.AbsoluteUri[2..]}") : endpoint;
            Console.WriteLine($"Verifying route, auth, and ssl by hitting ping endpoint at {pingEndpoint}...");
            var sw = Stopwatch.StartNew();
            HttpResponseMessage pingResponse = await httpClient.GetAsync(new Uri(pingEndpoint, "ws/ping"), cts.Token);
            if (pingResponse.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException($"Could not successfully ping wss. Status code {pingResponse.StatusCode} with body {await pingResponse.Content.ReadAsStringAsync(cts.Token)}");
            }
            Console.WriteLine($"Successful ping in {sw.ElapsedMilliseconds}ms.");
            Console.WriteLine();

            // Connect the web socket
            sw.Restart();
            await wsClient.ConnectAsync(new Uri(endpoint, "ws/connect"), cts.Token);
            Console.WriteLine($"Successful web socket connect in {sw.ElapsedMilliseconds}ms.");
            Console.WriteLine();
            byte[] buffer = new byte[1024 * 4];

            // Send the motd to get basic session info
            await wsClient.SendAsync(new CommandMessageMotdRequest().ToJsonBytes(), WebSocketMessageType.Text, endOfMessage: true, cts.Token);
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveAsync(buffer, cts.Token);
            if (!receiveResult.EndOfMessage)
            {
                // Using 4kb chunks, we should always receive the whole message
                await wsClient.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cts.Token);
                return;
            }
            CommandMessageMotdResponse motdResponse = buffer.FromJsonBytes<CommandMessageMotdResponse>(receiveResult.Count);
            Console.WriteLine("Current status:");
            Console.WriteLine(motdResponse.ToJsonString(indented: true));
            Console.WriteLine();

            // Close the websocket
            await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: default, cts.Token);
        }
    }
}
