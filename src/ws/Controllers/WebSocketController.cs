using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Tom.WebSocket.Filters;

namespace Unlimitedinf.Tom.WebSocket.Controllers
{
    [Route("/ws")]
    [ApiController]
    [RequiresToken]
    public sealed class WebSocketController : ControllerBase
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

        [HttpPost("connect")]
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
            byte[] buffer = new byte[1024 * 4];
            WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);

            while (!receiveResult.CloseStatus.HasValue)
            {
                receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);
            }

            await webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, cancellationToken);
        }
    }
}
