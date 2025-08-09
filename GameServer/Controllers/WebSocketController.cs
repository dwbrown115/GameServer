using System.Text.Json;
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
        // Log the incoming request
        Console.WriteLine(
            $"[WebSocketController] Step 1: Received auth request. Input: {JsonSerializer.Serialize(request)}"
        );

        var response = await _webSocketService.AuthenticateAsync(request);

        if (!response.Authenticated)
        {
            // Serialize the full response for better debugging
            Console.WriteLine(
                $"[WebSocketController] Step 4: Sending Unauthorized response. Reason: {response.Reason}"
            );
            return Unauthorized(response);
        }

        // Serialize the full response for better debugging
        Console.WriteLine(
            $"[WebSocketController] Step 4: Sending OK response. SessionId: {response.SessionId}"
        );
        return Ok(response);
    }
}
