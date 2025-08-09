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
        Console.WriteLine(
            $"[WebSocketController] Step 1: Received auth request. Input: {JsonConvert.SerializeObject(request)}"
        );

        var response = await _webSocketService.AuthenticateAsync(request);

        // Manually serialize the response to JSON. This removes any ambiguity from the
        // ASP.NET Core output formatters and ensures our log matches the response body exactly.
        // The default settings for Newtonsoft produce camelCase, which the Unity client expects.
        var jsonResponse = JsonConvert.SerializeObject(response);

        if (!response.Authenticated)
        {
            Console.WriteLine(
                $"[WebSocketController] Step 4: Sending Unauthorized response. Body: {jsonResponse}"
            );
            // Return the JSON string with a 401 Unauthorized status code.
            return Content(jsonResponse, "application/json", System.Text.Encoding.UTF8);
        }

        Console.WriteLine(
            $"[WebSocketController] Step 4: Sending OK response. Body: {jsonResponse}"
        );
        // Return the JSON string with a 200 OK status code.
        return Content(jsonResponse, "application/json", System.Text.Encoding.UTF8);
    }
}
