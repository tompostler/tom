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

        // Using 4KiB chunks, we should always receive the whole message (when sending text)
        private const int BufferSize = 1024 * 4;

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

            // Send the motd to get basic session info
            _ = await SendAndReceiveMotdAsync(wsClient, cts.Token);

            // List the current directory
            CommandMessageLsResponse lsResponse = await SendAndReceiveLsAsync(wsClient, cts.Token);
            DirectoryInfo remotePath = new(lsResponse.CurrentDirectory);

            await StartMessageLoopAsync(remotePath, wsClient, cts.Token);

            // Close the websocket
            await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: default, cts.Token);
        }

        private static string Prompt(DirectoryInfo remotePath)
        {
            Console.WriteLine($"Local: {Environment.CurrentDirectory}");
            Console.WriteLine($"Remote: {remotePath.FullName}");
            string input;
            do
            {
                Console.Write("> ");
                input = Console.ReadLine()?.Trim();
            }
            while (string.IsNullOrEmpty(input));
            return input;
        }

        private static async Task StartMessageLoopAsync(DirectoryInfo remotePath, ClientWebSocket wsClient, CancellationToken cancellationToken)
        {
            StringBuilder sb = new();
            _ = sb.AppendLine("Available commands:");
            _ = sb.AppendLine("   cd          Change the remote directory. To change the local directory, start the program in a different directory.");
            _ = sb.AppendLine("   ls          List the contents of the remote directory. Also displayed after using the cd command and connecting to the server.");
            _ = sb.AppendLine("   motd        Display the server status and welcome text.");
            _ = sb.AppendLine("   get <glob>  Receive remote file(s) from the server. Accepts wildcards and operates recursively.");
            _ = sb.AppendLine("   put <glob>  Send local file(s) to the server. Accepts wildcards and operates recursively.");
            _ = sb.AppendLine("   h, help     Display this helptext.");
            _ = sb.AppendLine("   q, quit     Close the websocket.");
            _ = sb.AppendLine("A couple notes on file copies:");
            _ = sb.AppendLine(" - Wildcard and recursive behavior is based on System.IO.DirectoryInfo.GetFiles(searchPattern, SearchOption.AllDirectories)");
            _ = sb.AppendLine(" - When recursed files are requested, the matching directory structure will be created at the destination");
            _ = sb.AppendLine(" - Duplicate files (based solely on filename) will be skipped");
            _ = sb.AppendLine(" - A summary will be displayed at the end");
            string helptext = sb.ToString();
            Console.WriteLine(helptext);

            while (true)
            {
                string input = Prompt(remotePath);
                string[] tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                switch (tokens.FirstOrDefault()?.ToLower())
                {
                    case "cd":
                        CommandMessageLsResponse cdResponse = await SendAndReceiveCdAsync(wsClient, tokens.Last(), cancellationToken);
                        remotePath = cdResponse != default ? new(cdResponse.CurrentDirectory) : remotePath;
                        break;

                    case "ls":
                        CommandMessageLsResponse lsResponse = await SendAndReceiveLsAsync(wsClient, cancellationToken);
                        remotePath = new(lsResponse.CurrentDirectory);
                        break;

                    case "motd":
                        CommandMessageMotdResponse motdResponse = await SendAndReceiveMotdAsync(wsClient, cancellationToken);
                        remotePath = new(motdResponse.CurrentDirectory);
                        break;

                    case "h" or "help":
                        Console.WriteLine(helptext);
                        break;

                    case "q" or "quit":
                        return;

                    default:
                        Console.WriteLine($"Could not interpret input [{input}]");
                        Console.WriteLine();
                        break;
                }
            }
        }

        private static async Task<CommandMessageLsResponse> SendAndReceiveCdAsync(ClientWebSocket wsClient, string target, CancellationToken cancellationToken)
        {
            await wsClient.SendAsync(new CommandMessageCd { Target = target }.ToJsonBytes(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveAsync(buffer, cancellationToken);
            if (!receiveResult.EndOfMessage)
            {
                await wsClient.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cancellationToken);
                throw new NotImplementedException("Text message not sent in full.");
            }

            string messageText = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            CommandMessage commandMessage = messageText.FromJsonString<CommandMessage>();

            if (commandMessage.Type == CommandType.ls)
            {
                CommandMessageLsResponse lsResponse = messageText.FromJsonString<CommandMessageLsResponse>();
                Console.WriteLine("Current directory contents on remote:");
                Output.WriteTable(
                    lsResponse.Dirs.Union(lsResponse.Files).Select(x => new { x.Name, x.Modified, Length = x.Length?.AsBytesToFriendlyString() }),
                    nameof(CommandMessageLsResponse.TrimmedFileSystemObjectInfo.Name),
                    nameof(CommandMessageLsResponse.TrimmedFileSystemObjectInfo.Modified),
                    nameof(CommandMessageLsResponse.TrimmedFileSystemObjectInfo.Length));
                return lsResponse;
            }
            else if (commandMessage.Type == CommandType.error)
            {
                CommandMessageErrorResponse errorMessage = messageText.FromJsonString<CommandMessageErrorResponse>();
                Console.WriteLine($"ERROR:\n{errorMessage.Payload}");
            }
            else
            {
                Console.WriteLine($"ERROR:\nMessage was of unexpected type {commandMessage.Type}");
            }
            return default;
        }

        private static async Task<CommandMessageLsResponse> SendAndReceiveLsAsync(ClientWebSocket wsClient, CancellationToken cancellationToken)
        {
            await wsClient.SendAsync(new CommandMessageLsRequest().ToJsonBytes(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveAsync(buffer, cancellationToken);
            if (!receiveResult.EndOfMessage)
            {
                await wsClient.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cancellationToken);
                throw new NotImplementedException("Text message not sent in full.");
            }

            CommandMessageLsResponse lsResponse = buffer.FromJsonBytes<CommandMessageLsResponse>(receiveResult.Count);
            Console.WriteLine("Current directory contents on remote:");
            Output.WriteTable(
                lsResponse.Dirs.Union(lsResponse.Files).Select(x => new { x.Name, x.Modified, Length = x.Length?.AsBytesToFriendlyString() }),
                nameof(CommandMessageLsResponse.TrimmedFileSystemObjectInfo.Name),
                nameof(CommandMessageLsResponse.TrimmedFileSystemObjectInfo.Modified),
                nameof(CommandMessageLsResponse.TrimmedFileSystemObjectInfo.Length));
            return lsResponse;
        }

        private static async Task<CommandMessageMotdResponse> SendAndReceiveMotdAsync(ClientWebSocket wsClient, CancellationToken cancellationToken)
        {
            await wsClient.SendAsync(new CommandMessageMotdRequest().ToJsonBytes(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveAsync(buffer, cancellationToken);
            if (!receiveResult.EndOfMessage)
            {
                await wsClient.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cancellationToken);
                throw new NotImplementedException("Text message not sent in full.");
            }

            CommandMessageMotdResponse motdResponse = buffer.FromJsonBytes<CommandMessageMotdResponse>(receiveResult.Count);
            Console.WriteLine("Current status:");
            Console.WriteLine(motdResponse.ToJsonString(indented: true));
            Console.WriteLine();
            return motdResponse;
        }
    }
}
