using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Controllers;

[ApiController]
[Route("ws")]
public class WebSocketController : ControllerBase
{
    private readonly IWebSocketService _webSocketService;

    public WebSocketController(IWebSocketService webSocketService)
    {
        _webSocketService = webSocketService;
    }

    [HttpPost("auth")]
    [ProducesResponseType(typeof(WebSocketAuthResponse), 200)]
    [ProducesResponseType(typeof(WebSocketAuthResponse), 401)]
    public async Task<IActionResult> Authenticate([FromBody] WebSocketAuthRequest request)
    {
        var response = await _webSocketService.AuthenticateAsync(request);

        if (!response.Authenticated)
        {
            return Unauthorized(response);
        }

        return Ok(response);
    }
}
