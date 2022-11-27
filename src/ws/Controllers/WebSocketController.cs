using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Tom.WebSocket.Filters;
using Unlimitedinf.Tom.WebSocket.Models;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Tom.WebSocket.Controllers
{
    [Route("/ws")]
    [ApiController]
    [RequiresToken]
    internal sealed class WebSocketController : ControllerBase
    {
        private readonly Options options;
        private readonly Status status;

        public WebSocketController(
            Options options,
            Status status)
        {
            this.options = options;
            this.status = status;
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
                    this.status,
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
            WebSocketState state = new() { CurrentDirectory = new(Environment.CurrentDirectory) };

            byte[] buffer = new byte[1024 * 4];

            // We wait for the client to send the first message
            WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);

            while (!receiveResult.CloseStatus.HasValue)
            {
                // If the message type is text, then it's a command and we can buffer it entirely into memory to decide what to do with it
                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    // Using 4kb chunks, we should always receive the whole message
                    if (!receiveResult.EndOfMessage)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Text message not sent in full.", cancellationToken);
                        return;
                    }

                    _ = Interlocked.Increment(ref this.status.TextMessagesReceived);
                    string messageText = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                    CommandMessageBase commandMessageBase = messageText.FromJsonString<CommandMessageBase>();
                    switch (commandMessageBase.Type)
                    {
                        case CommandType.motd:
                            await webSocket.SendAsync(
                                new CommandMessageMotdResponse
                                {
                                    CurrentDirectory = state.CurrentDirectory.FullName,
                                    BytesPerSecondLimit = this.options.BytesPerSecondLimit,
                                    Status = this.status
                                }.ToJsonBytes(),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                cancellationToken);
                            _ = Interlocked.Increment(ref this.status.TextMessagesSent);
                            break;

                        case CommandType.cd:
                        //TODO

                        case CommandType.ls:
                        //TODO

                        default:
                            await webSocket.SendAsync(
                                new CommandMessageUnknown { Payload = $"{nameof(CommandType)}.{commandMessageBase.Type} is not mapped for handling." }.ToJsonBytes(),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                cancellationToken);
                            _ = Interlocked.Increment(ref this.status.TextMessagesSent);
                            break;
                    }
                }

                // If it's binary, then we better be expecting a file to be coming in
                else if (receiveResult.MessageType == WebSocketMessageType.Binary)
                {
                    if (state.ReceivingFile == default)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Binary message sent without first establishing file transfer information.", cancellationToken);
                        return;
                    }

                    // Process the file transfer
                    //TODO
                }

                // Get the next message
                receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);
            }

            await webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, cancellationToken);
        }
    }
}
