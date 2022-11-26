using Microsoft.Net.Http.Headers;
using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Utilities;

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

            Option<string> sslCertThumbprintOpt = new(
                "--ssl-cert-thumbprint",
                "Used in scenarios where the HTTPS certificate is not expected to match the hostname of the endpoint.");
            command.AddOption(sslCertThumbprintOpt);

            Option<TimeSpan> maxSessionDurationOpt = new(
                "--max-session-duration",
                () => TimeSpan.FromDays(3),
                "After this amount of time, the session will be aborted with the server.");
            command.AddOption(maxSessionDurationOpt);

            command.SetHandler(HandleAsync, endpointArg, workingDirOpt, sslCertThumbprintOpt, maxSessionDurationOpt);
            return command;
        }

        private static async Task HandleAsync(Uri endpoint, DirectoryInfo workingDir, string sslCertThumbprint, TimeSpan maxSessionDuration)
        {
            Environment.CurrentDirectory = workingDir.FullName;
            using CancellationTokenSource cts = new(maxSessionDuration);
            string token = Input.GetString("Authorization token");
            Console.WriteLine();

            // For non-endpoint matching or self-signed certificates, validate them manually
            // (self-signed certificates are perfectly valid for TLS as long as you are validating the certificate is the one you expect)
            bool serverCertificateValidationCallback(X509Certificate certificate, SslPolicyErrors sslPolicyErrors)
            {
                // If there are no errors, then the endpoint is completely valid (regardless of us providing a specific thumbprint)
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;
                }

                // Only return true if the thumprint of the certificate matches what we're expecting
                return string.Equals(sslCertThumbprint, certificate?.GetCertHashString());
            }

            // Set up the basic httpClient
            HttpClient httpClient = new();
            if (!string.IsNullOrWhiteSpace(sslCertThumbprint))
            {
                HttpClientHandler handler = new()
                {
                    ServerCertificateCustomValidationCallback = (_, certificate, _, sslPolicyErrors) => serverCertificateValidationCallback(certificate, sslPolicyErrors)
                };
                httpClient = new(handler);
            }
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);
            
            // Set up the web socket client
            ClientWebSocket client = new();
            if (!string.IsNullOrWhiteSpace(sslCertThumbprint))
            {
                client.Options.RemoteCertificateValidationCallback = (_, certificate, _, sslPolicyErrors) => serverCertificateValidationCallback(certificate, sslPolicyErrors);
            }
            client.Options.SetRequestHeader(HeaderNames.Authorization, httpClient.DefaultRequestHeaders.Authorization.ToString());

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

            //await client.ConnectAsync(new Uri(endpoint, "ws/connect"), cts.Token);
        }
    }
}
