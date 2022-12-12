using Microsoft.Net.Http.Headers;
using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Tom.WebSocket.Contracts;
using Unlimitedinf.Utilities;
using Unlimitedinf.Utilities.Extensions;
using Unlimitedinf.Utilities.Logging;

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

        // Using 16KiB chunks, we should always receive the whole message (when sending text)
        private const int BufferSize = 1024 * 16;

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
            string remotePath = lsResponse.CurrentDirectory;

            await StartMessageLoopAsync(remotePath, wsClient, cts.Token);

            // Close the websocket
            await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: default, cts.Token);
        }

        private static async Task StartMessageLoopAsync(string remotePath, ClientWebSocket wsClient, CancellationToken cancellationToken)
        {
            DirectoryInfo localPath = new(Environment.CurrentDirectory);
            StringBuilder sb = new();
            _ = sb.AppendLine("Available commands:");
            _ = sb.AppendLine("   cd          Change the directory. Will only allow changing to remote directories that exist, and the local directory structure will be forced to match.");
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
                // Prompt for input
                Console.WriteLine($"Local:  {localPath.FullName}");
                Console.WriteLine($"Remote: {remotePath}");
                string input;
                do
                {
                    Console.Write("> ");
                    input = Console.ReadLine()?.Trim();
                }
                while (string.IsNullOrEmpty(input));

                switch (input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()?.ToLower())
                {
                    case "cd":
                        CommandMessageLsResponse cdResponse = await SendAndReceiveCdAsync(wsClient, input.Substring("cd ".Length), cancellationToken);
                        if (cdResponse != default)
                        {
                            localPath = new(Path.Join(localPath.FullName, input.Substring("cd ".Length)));
                            localPath.Create();

                            remotePath = cdResponse.CurrentDirectory;
                        }
                        break;

                    case "ls":
                        CommandMessageLsResponse lsResponse = await SendAndReceiveLsAsync(wsClient, cancellationToken);
                        remotePath = lsResponse.CurrentDirectory;
                        break;

                    case "get":
                        await HandleGetAsync(wsClient, localPath, input.Substring("get ".Length), cancellationToken);
                        break;

                    case "put":
                        await HandlePutAsync(wsClient, localPath, input.Substring("put ".Length), cancellationToken);
                        break;

                    case "motd":
                        CommandMessageMotdResponse motdResponse = await SendAndReceiveMotdAsync(wsClient, cancellationToken);
                        remotePath = motdResponse.CurrentDirectory;
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

        private static async Task<WebSocketReceiveResult> ReceiveWholeTextMessageAsync(this ClientWebSocket wsClient, byte[] buffer, CancellationToken cancellationToken)
        {
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveAsync(buffer, cancellationToken);
            if (!receiveResult.EndOfMessage)
            {
                await wsClient.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cancellationToken);
                throw new NotImplementedException("Text message not sent in full.");
            }
            return receiveResult;
        }

        /// <summary>
        /// Getting a list of files from the server follows the following process:
        /// 0. An error message may be sent if there's nothing to send as a <see cref="CommandMessageErrorResponse"/>
        /// 1. A list of all files to be sent is sent as a <see cref="CommandMessageGetResponse"/>
        /// 2. On a loop until the queue is exhausted:
        ///     1. The file is sent
        ///     2. A summary of the file sent (including the hash for verification) is sent as a <see cref="CommandMessageGetEndResponse"/>
        /// </summary>
        private static async Task HandleGetAsync(ClientWebSocket wsClient, DirectoryInfo localPath, string target, CancellationToken cancellationToken)
        {
            await wsClient.SendAsync(new CommandMessageGetRequest { Target = target }.ToJsonBytes(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveWholeTextMessageAsync(buffer, cancellationToken);

            string messageText = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            CommandMessage commandMessage = messageText.FromJsonString<CommandMessage>();
            if (commandMessage.Type == CommandType.error)
            {
                CommandMessageErrorResponse errorMessage = messageText.FromJsonString<CommandMessageErrorResponse>();
                Console.WriteLine($"ERROR:\n{errorMessage.Payload}");
                return;
            }
            else if (commandMessage.Type != CommandType.get)
            {
                Console.WriteLine($"ERROR:\nMessage was of unexpected type {commandMessage.Type}");
                return;
            }

            CommandMessageGetResponse getResponse = messageText.FromJsonString<CommandMessageGetResponse>();
            Console.WriteLine("Starting transfer of the following files:");
            Output.WriteTable(
                getResponse.Files.Select(x => new { x.Name, x.Modified, Length = x.Length.AsBytesToFriendlyString() }),
                nameof(TrimmedFileSystemObjectInfo.Name),
                nameof(TrimmedFileSystemObjectInfo.Modified),
                nameof(TrimmedFileSystemObjectInfo.Length));

            ConsoleFileProgressLogger progressLogger = new(default, 0, getResponse.Files.Count + 1, getResponse.Files.Sum(x => x.Length));
            foreach (TrimmedFileSystemObjectInfo incomingFile in getResponse.Files)
            {
                // Set up the target to write to
                FileInfo fileInfo = new(Path.Join(localPath.FullName, incomingFile.Name));
                _ = Directory.CreateDirectory(fileInfo.DirectoryName);

                progressLogger.ResetCurrentFile(fileInfo.FullName.Substring(localPath.FullName.Length), incomingFile.Length);

                // First receive the file
                var sha256 = SHA256.Create();
                using (FileStream fs = fileInfo.OpenWrite())
                {
                    do
                    {
                        receiveResult = await wsClient.ReceiveAsync(buffer, cancellationToken);
                        _ = sha256.TransformBlock(buffer, inputOffset: 0, receiveResult.Count, outputBuffer: default, outputOffset: default);

                        await fs.WriteAsync(buffer.AsMemory(0, receiveResult.Count), cancellationToken);
                        progressLogger.AddProgress(receiveResult.Count);
                    }
                    while (!receiveResult.EndOfMessage);
                    _ = sha256.TransformFinalBlock(Array.Empty<byte>(), default, default);
                }

                // Get the message to verify the hash
                receiveResult = await wsClient.ReceiveWholeTextMessageAsync(buffer, cancellationToken);
                CommandMessageGetEndResponse getEndResponse = buffer.FromJsonBytes<CommandMessageGetEndResponse>(receiveResult.Count);
                string actualHashSHA256 = sha256.Hash.ToLowercaseHash();
                if (!string.Equals(getEndResponse.HashSHA256, actualHashSHA256, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"ERROR:\nFor file {fileInfo.FullName}, exepected hash was {getEndResponse.HashSHA256} but actual hash was {actualHashSHA256}.\nTerminating socket.");
                    await wsClient.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Did not receive correct data.", cancellationToken);
                    throw new NotImplementedException("Did not receive correct data.");
                }
                else
                {
                    Console.WriteLine($"Validated {fileInfo.FullName} had expected hash.");
                }
            }
            progressLogger.MarkComplete();
        }

        /// <summary>
        /// Putting a list of files from the client follows the following process:
        /// 0. An error message may be shown if there's nothing to send
        /// 1. A list of all files to be sent is sent as a <see cref="CommandMessagePutRequest"/>
        /// 2. On a loop until the queue is exhausted:
        ///     1. The file is sent
        ///     2. A summary of the file sent (including the hash for verification) is sent as a <see cref="CommandMessagePutEndRequest"/>
        /// </summary>
        private static async Task HandlePutAsync(ClientWebSocket wsClient, DirectoryInfo localPath, string target, CancellationToken cancellationToken)
        {
            FileInfo[] filesToSend = localPath.GetFiles(target, SearchOption.AllDirectories).Where(x => x.Length > 0).OrderBy(x => x.FullName).ToArray();
            if (filesToSend.Length == 0)
            {
                Console.WriteLine("ERROR: No files found.");
                return;
            }
            Console.WriteLine($"Sending {filesToSend.Length} files: {string.Join(", ", filesToSend.Select(x => x.FullName))}");
            await wsClient.SendAsync(
                new CommandMessagePutRequest
                {
                    Files = filesToSend.Select(
                        x => new TrimmedFileSystemObjectInfo
                        {
                            Type = "File",
                            Name = x.FullName.Substring(localPath.FullName.Length),
                            Modified = x.LastWriteTime,
                            Length = x.Length
                        }).ToList()
                }.ToJsonBytes(),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            ConsoleFileProgressLogger progressLogger = new(default, 0, filesToSend.Length + 1, filesToSend.Sum(x => x.Length));
            foreach (FileInfo fileToSend in filesToSend)
            {
                progressLogger.ResetCurrentFile(fileToSend.Name, fileToSend.Length);

                // First send the file
                var sha256 = SHA256.Create();
                byte[] buffer = new byte[BufferSize];
                using (FileStream fs = fileToSend.OpenRead())
                {
                    while (fs.Position < fs.Length)
                    {
                        int bytesRead = await fs.ReadAsync(buffer, cancellationToken);
                        _ = sha256.TransformBlock(buffer, inputOffset: 0, bytesRead, outputBuffer: default, outputOffset: 0);

                        await wsClient.SendAsync(buffer.AsMemory(0, bytesRead), WebSocketMessageType.Binary, endOfMessage: bytesRead != buffer.Length, cancellationToken);
                        progressLogger.AddProgress(bytesRead);
                    }
                }
                _ = sha256.TransformFinalBlock(Array.Empty<byte>(), default, default);

                // Once that's complete, send the hash
                await wsClient.SendAsync(
                    new CommandMessagePutEndRequest
                    {
                        HashSHA256 = sha256.Hash.ToLowercaseHash()
                    }.ToJsonBytes(),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
            }
            progressLogger.MarkComplete();
        }

        private static void PrintLsResponse(CommandMessageLsResponse lsResponse)
        {
            Output.WriteTable(
                lsResponse.Dirs.Union(lsResponse.Files).Select(x => new { x.Name, x.Modified, x.Type, Length = x.Length.AsBytesToFriendlyString() }),
                nameof(TrimmedFileSystemObjectInfo.Name),
                nameof(TrimmedFileSystemObjectInfo.Modified),
                nameof(TrimmedFileSystemObjectInfo.Type),
                nameof(TrimmedFileSystemObjectInfo.Length));
        }

        private static async Task<CommandMessageLsResponse> SendAndReceiveCdAsync(ClientWebSocket wsClient, string target, CancellationToken cancellationToken)
        {
            await wsClient.SendAsync(new CommandMessageCd { Target = target }.ToJsonBytes(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveWholeTextMessageAsync(buffer, cancellationToken);

            string messageText = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            CommandMessage commandMessage = messageText.FromJsonString<CommandMessage>();

            if (commandMessage.Type == CommandType.ls)
            {
                CommandMessageLsResponse lsResponse = messageText.FromJsonString<CommandMessageLsResponse>();
                Console.WriteLine("Current directory contents on remote:");
                PrintLsResponse(lsResponse);
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
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveWholeTextMessageAsync(buffer, cancellationToken);

            CommandMessageLsResponse lsResponse = buffer.FromJsonBytes<CommandMessageLsResponse>(receiveResult.Count);
            Console.WriteLine("Current directory contents on remote:");
            PrintLsResponse(lsResponse);
            return lsResponse;
        }

        private static async Task<CommandMessageMotdResponse> SendAndReceiveMotdAsync(ClientWebSocket wsClient, CancellationToken cancellationToken)
        {
            await wsClient.SendAsync(new CommandMessageMotdRequest().ToJsonBytes(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult = await wsClient.ReceiveWholeTextMessageAsync(buffer, cancellationToken);

            CommandMessageMotdResponse motdResponse = buffer.FromJsonBytes<CommandMessageMotdResponse>(receiveResult.Count);
            Console.WriteLine("Current status:");
            Console.WriteLine(motdResponse.ToJsonString(indented: true));
            Console.WriteLine();
            return motdResponse;
        }
    }
}
