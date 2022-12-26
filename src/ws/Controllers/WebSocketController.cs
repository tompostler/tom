using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Tom.WebSocket.Contracts;
using Unlimitedinf.Tom.WebSocket.Filters;
using Unlimitedinf.Utilities;
using Unlimitedinf.Utilities.Extensions;
using Unlimitedinf.Utilities.IO;

namespace Unlimitedinf.Tom.WebSocket.Controllers
{
    [Route("/ws")]
    [ApiController]
    [RequiresToken]
    public sealed class WebSocketController : ControllerBase
    {
        // Using 128KiB chunks, we should always receive the whole message
        private const int BufferSize = 1024 * 128;

        private readonly Options options;
        private readonly ILogger<WebSocketController> logger;

        public WebSocketController(
            Options options,
            ILogger<WebSocketController> logger)
        {
            this.options = options;
            this.logger = logger;
        }

        [HttpGet("ping")]
        public IActionResult Ping() => this.NoContent();

        [HttpGet("debug")]
        public IActionResult Debug()
        {
            SortedList<string, string> environmentVariables = new();
            IDictionary unsortedEnvironmentVariables = Environment.GetEnvironmentVariables();
            foreach (object key in unsortedEnvironmentVariables.Keys)
            {
                environmentVariables.Add(key as string, unsortedEnvironmentVariables[key] as string);
            }

            return this.Ok(
                new
                {
                    Environment.CurrentDirectory,
                    this.options,
                    status = Status.Instance,
                    environmentVariables
                });
        }

        [HttpGet("connect")]
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            // Since we don't return IActionResult from web sockets, we need to set the status code on validation directly
            if (!this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                this.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await this.HttpContext.Response.WriteAsync("Web socket request expected.", cancellationToken);
            }

            else
            {
                using System.Net.WebSockets.WebSocket webSocket = await this.HttpContext.WebSockets.AcceptWebSocketAsync();
                await this.HandleAsync(webSocket, cancellationToken);
            }
        }

        private async Task HandleAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken)
        {
            DirectoryInfo localDir = new(Environment.CurrentDirectory);
            byte[] buffer = new byte[BufferSize];

            // We wait for the client to send the first message
            WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);

            while (!receiveResult.CloseStatus.HasValue)
            {
                // If the message type is text, then it's a command and we can buffer it entirely into memory to decide what to do with it
                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    if (!receiveResult.EndOfMessage)
                    {
                        this.logger.LogWarning("Closing web socket since text message did not fit in buffer.");
                        await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cancellationToken);
                        return;
                    }

                    _ = Interlocked.Increment(ref Status.Instance.textMessagesReceived);
                    string messageText = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                    CommandMessage commandMessage = messageText.FromJsonString<CommandMessage>();
                    this.logger.LogInformation($"Handling {commandMessage.Type}");
                    switch (commandMessage.Type)
                    {
                        case CommandType.motd:
                            await webSocket.SendAsync(
                                new CommandMessageMotdResponse
                                {
                                    Message = $"Hello {Rando.GetString(Rando.RandomType.Name)} on this fine {DateTime.Now.DayOfWeek}. When I asked the magic 8 ball 'Will this session be successful?', it responded: {Rando.GetString(Rando.RandomType.EightBall)}",
                                    CurrentDirectory = localDir.FullName,
                                    MegabitPerSecondLimit = this.options.BytesPerSecondLimit / 1_000_000d * 8,
                                    Status = Status.Instance
                                }.ToJsonBytes(),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                cancellationToken);
                            _ = Interlocked.Increment(ref Status.Instance.textMessagesSent);
                            break;

                        case CommandType.cd:
                            CommandMessageCd commandMessageCd = messageText.FromJsonString<CommandMessageCd>();
                            this.logger.LogInformation($"Attempting to cd to {commandMessageCd.Target}");
                            DirectoryInfo[] currentDirs = localDir.GetDirectories();
                            DirectoryInfo matchingDir = currentDirs.FirstOrDefault(x => string.Equals(x.Name, commandMessageCd.Target, StringComparison.Ordinal));
                            if (commandMessageCd.Target == ".." && localDir.FullName.Length > new DirectoryInfo(Environment.CurrentDirectory).FullName.Length)
                            {
                                // Only allow parent traversal if the current directory full name length is longer than the root directory of the process
                                localDir = localDir.Parent;
                                goto case CommandType.ls;
                            }
                            else if (matchingDir == default)
                            {
                                await webSocket.SendAsync(
                                    new CommandMessageErrorResponse { Payload = $"[{commandMessageCd.Target}] directory does not exist." }.ToJsonBytes(),
                                    WebSocketMessageType.Text,
                                    endOfMessage: true,
                                    cancellationToken);
                                _ = Interlocked.Increment(ref Status.Instance.textMessagesSent);
                                break;
                            }
                            else
                            {
                                localDir = matchingDir;
                                goto case CommandType.ls;
                            }

                        case CommandType.ls:
                            await webSocket.SendAsync(
                                new CommandMessageLsResponse
                                {
                                    CurrentDirectory = localDir.FullName,
                                    Dirs = localDir.GetDirectories()
                                        .Select(
                                            x => new TrimmedFileSystemObjectInfo
                                            {
                                                Type = "Dir",
                                                Name = x.Name,
                                                Modified = x.LastWriteTime,
                                                Length = x.GetFiles("*", SearchOption.AllDirectories).Sum(x => x.Length)
                                            }).ToList(),
                                    Files = localDir.GetFiles()
                                        .Select(
                                            x => new TrimmedFileSystemObjectInfo
                                            {
                                                Type = "File",
                                                Name = x.Name,
                                                Length = x.Length,
                                                Modified = x.LastWriteTime
                                            }).ToList()
                                }.ToJsonBytes(),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                cancellationToken);
                            _ = Interlocked.Increment(ref Status.Instance.textMessagesSent);
                            break;

                        case CommandType.get:
                            CommandMessageGetRequest commandMessageGetRequest = messageText.FromJsonString<CommandMessageGetRequest>();
                            this.logger.LogInformation($"Attempting to get non-empty files based on {commandMessageGetRequest.Target}");
                            FileInfo[] filesToSend = localDir.GetFiles(commandMessageGetRequest.Target, SearchOption.AllDirectories).Where(x => x.Length > 0).OrderBy(x => x.FullName).ToArray();
                            if (filesToSend.Length == 0)
                            {
                                this.logger.LogWarning("No files found.");
                                await webSocket.SendAsync(
                                    new CommandMessageErrorResponse { Payload = $"Could not find any files in {localDir.FullName} for {commandMessageGetRequest.Target}" }.ToJsonBytes(),
                                    WebSocketMessageType.Text,
                                    endOfMessage: true,
                                    cancellationToken);
                                _ = Interlocked.Increment(ref Status.Instance.textMessagesSent);
                            }
                            else
                            {
                                this.logger.LogInformation($"Sending {filesToSend.Length} files: {string.Join(", ", filesToSend.Select(x => x.FullName))}");
                                await webSocket.SendAsync(
                                    new CommandMessageGetResponse
                                    {
                                        Files = filesToSend.Select(
                                            x => new TrimmedFileSystemObjectInfo
                                            {
                                                Type = "File",
                                                Name = x.FullName.Substring(localDir.FullName.Length),
                                                Modified = x.LastWriteTime,
                                                Length = x.Length
                                            }).ToList()
                                    }.ToJsonBytes(),
                                    WebSocketMessageType.Text,
                                    endOfMessage: true,
                                    cancellationToken);
                                _ = Interlocked.Increment(ref Status.Instance.textMessagesSent);

                                Queue<FileInfo> sendingFiles = new(filesToSend);
                                await this.HandleFileGetAsync(webSocket, sendingFiles, cancellationToken);
                            }
                            break;

                        case CommandType.put:
                            CommandMessagePutRequest commandMessagePutRequest = messageText.FromJsonString<CommandMessagePutRequest>();
                            Queue<FileInfo> receivingFiles = new(commandMessagePutRequest.Files.Select(x => new FileInfo(Path.Join(localDir.FullName, x.Name))));
                            await this.HandleFilePutAsync(webSocket, receivingFiles, cancellationToken);
                            break;

                        default:
                            await webSocket.SendAsync(
                                new CommandMessageErrorResponse { Payload = $"{nameof(CommandType)}.{commandMessage.Type} is not mapped for handling." }.ToJsonBytes(),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                cancellationToken);
                            _ = Interlocked.Increment(ref Status.Instance.textMessagesSent);
                            break;
                    }
                }

                // We shouldn't be receiving random binary requests
                else if (receiveResult.MessageType == WebSocketMessageType.Binary)
                {
                    this.logger.LogWarning("Closing web socket since unexpected binary message sent.");
                    await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Binary message sent without first establishing file transfer information.", cancellationToken);
                    return;
                }

                // Get the next message
                receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);
            }

            await webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, cancellationToken);
        }

        /// <summary>
        /// Getting a list of files from the server follows the following process:
        /// 1. A list of all files to be sent is sent as a <see cref="CommandMessageGetResponse"/>
        /// 2. On a loop until the queue is exhausted:
        ///     1. The file is sent
        ///     2. A summary of the file sent (including the hash for verification) is sent as a <see cref="CommandMessageGetEndResponse"/>
        /// </summary>
        private async Task HandleFileGetAsync(System.Net.WebSockets.WebSocket webSocket, Queue<FileInfo> sendingFiles, CancellationToken cancellationToken)
        {
            while (sendingFiles.TryDequeue(out FileInfo fileToSend))
            {
                this.logger.LogInformation($"Sending {fileToSend.FullName}");

                // First send the file
                var sha256 = SHA256.Create();
                using (FileStream fs = fileToSend.OpenRead())
                using (ThrottledStream ts = new(fs, this.options.BytesPerSecondLimit))
                {
                    byte[] buffer = new byte[BufferSize];
                    while (fs.Position < fs.Length)
                    {
                        int bytesRead = await ts.ReadAsync(buffer, cancellationToken);
                        _ = sha256.TransformBlock(buffer, inputOffset: 0, bytesRead, outputBuffer: default, outputOffset: 0);
                        _ = Interlocked.Add(ref Status.Instance.BinaryBytesSent, (ulong)bytesRead);
                        await webSocket.SendAsync(buffer.AsMemory(0, bytesRead), WebSocketMessageType.Binary, endOfMessage: bytesRead != buffer.Length, cancellationToken);
                    }
                }
                _ = sha256.TransformFinalBlock(Array.Empty<byte>(), default, default);

                // Once that's complete, send the hash
                await webSocket.SendAsync(
                    new CommandMessageGetEndResponse
                    {
                        HashSHA256 = sha256.Hash.ToLowercaseHash()
                    }.ToJsonBytes(),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
                _ = Interlocked.Increment(ref Status.Instance.textMessagesSent);
            }
        }

        /// <summary>
        /// Sending a list of files from the client follows the following process:
        /// 1. A list of all files to be sent is sent as a <see cref="CommandMessagePutRequest"/>
        /// 2. On a loop until the queue is exhausted:
        ///     1. The file is sent
        ///     2. A summary of the file sent (including the hash for verification) is sent as a <see cref="CommandMessagePutEndRequest"/>
        /// </summary>
        private async Task HandleFilePutAsync(System.Net.WebSockets.WebSocket webSocket, Queue<FileInfo> receivingFiles, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult;
            while (receivingFiles.TryDequeue(out FileInfo fileToReceive))
            {
                this.logger.LogInformation($"Receiving {fileToReceive.FullName}");
                _ = Directory.CreateDirectory(fileToReceive.DirectoryName);

                // First receive the file
                var sha256 = SHA256.Create();
                using (FileStream fs = fileToReceive.OpenWrite())
                using (ThrottledStream ts = new(fs, this.options.BytesPerSecondLimit))
                {
                    do
                    {
                        receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);
                        _ = sha256.TransformBlock(buffer, inputOffset: 0, receiveResult.Count, outputBuffer: default, outputOffset: default);
                        _ = Interlocked.Add(ref Status.Instance.BinaryBytesReceived, (ulong)receiveResult.Count);
                        await ts.WriteAsync(buffer.AsMemory(0, receiveResult.Count), cancellationToken);
                    }
                    while (!receiveResult.EndOfMessage);
                }
                _ = sha256.TransformFinalBlock(Array.Empty<byte>(), default, default);

                // Get the message to verify the hash
                receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);
                _ = Interlocked.Increment(ref Status.Instance.textMessagesReceived);
                if (!receiveResult.EndOfMessage)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cancellationToken);
                    throw new InvalidOperationException("Text message not sent in full.");
                }
                CommandMessagePutEndRequest putEndRequest = buffer.FromJsonBytes<CommandMessagePutEndRequest>(receiveResult.Count);
                string actualHashSHA256 = sha256.Hash.ToLowercaseHash();
                if (!string.Equals(putEndRequest.HashSHA256, actualHashSHA256, StringComparison.OrdinalIgnoreCase))
                {
                    this.logger.LogError($"For file {fileToReceive.FullName}, exepected hash was {putEndRequest.HashSHA256} but actual hash was {actualHashSHA256}. Terminating socket.");
                    await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Did not receive correct data.", cancellationToken);
                    throw new InvalidOperationException("Did not receive correct data.");
                }
                else
                {
                    this.logger.LogInformation($"Validated {fileToReceive.FullName} had expected hash.");
                }
            }
        }
    }
}
