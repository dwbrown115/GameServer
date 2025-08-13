using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json; // Use Newtonsoft for logging consistency
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
        // Console.WriteLine(
        //     $"[WebSocketController] Step 1: Received auth request. Input: {JsonConvert.SerializeObject(request)}"
        // );

        var response = await _webSocketService.AuthenticateAsync(request);

        if (!response.Authenticated)
        {
            // Console.WriteLine($"[WebSocketController] Step 4: Sending Unauthorized response. Body: {JsonConvert.SerializeObject(response)}");
            return Unauthorized(response);
        }

        // Console.WriteLine($"[WebSocketController] Step 4: Sending OK response. Body: {JsonConvert.SerializeObject(response)}");
        // Using Ok(response) is the standard and recommended way. It lets the framework
        // handle content negotiation and correctly formats the response.
        return Ok(response);
    }
}
