using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.WebSocket.Controllers
{
    [Route("/ws")]
    [ApiController]
    public sealed class WebSocketController : ControllerBase
    {
        [HttpGet("ping")]
        public IActionResult Ping() => this.NoContent();

        [HttpPost("connect")]
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            // Since we don't return IActionResult from web sockets, we need to set the status code on validation directly
            if (!string.Equals(this.HttpContext.Request.Headers.Authorization.FirstOrDefault(), Program.Password))
            {
                this.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await this.HttpContext.Response.WriteAsync("PSK required.", cancellationToken);
            }
            else if (!this.HttpContext.WebSockets.IsWebSocketRequest)
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
