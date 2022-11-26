using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
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

        public WebSocketController(Options options)
        {
            this.options = options;
        }

        [HttpGet("ping")]
        public IActionResult Ping() => this.NoContent();

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

        private Task HandleAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
