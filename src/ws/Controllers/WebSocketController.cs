using Microsoft.AspNetCore.Mvc;

namespace Unlimitedinf.Tom.WebSocket.Controllers
{
    [Route("/ws")]
    [ApiController]
    public sealed class WebSocketController : ControllerBase
    {
        [HttpGet("ping")]
        public IActionResult Ping() => this.NoContent();
    }
}
