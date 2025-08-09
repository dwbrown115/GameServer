using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Controllers;

[ApiController]
[Route("player")]
public class PlayerController : ControllerBase
{
    private readonly IPlayerService _playerService;

    public PlayerController(IPlayerService playerService)
    {
        _playerService = playerService;
    }

    // ... Get method remains the same ...
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PlayerResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get([FromRoute] string id)
    {
        var playerResponse = await _playerService.GetPlayerAsync(id);
        if (playerResponse == null)
        {
            return NotFound(new { message = $"Player with ID '{id}' not found." });
        }
        return Ok(playerResponse);
    }

    /// <summary>
    /// Updates player data after validating their session.
    /// </summary>
    [HttpPatch("update")]
    [ProducesResponseType(typeof(PlayerChangeResponse), 200)]
    [ProducesResponseType(typeof(object), 400)] // For data validation errors
    [ProducesResponseType(typeof(object), 401)] // For auth errors
    public async Task<IActionResult> UpdatePlayer([FromBody] PlayerChangeRequest request)
    {
        Console.WriteLine($"[PlayerController] Received update request: {JsonConvert.SerializeObject(request)}");
        var changeResponse = await _playerService.UpdatePlayerDataAsync(request);

        if (!changeResponse.Success)
        {
            // Distinguish between an authentication failure and a data validation failure.
            if (changeResponse.Message.Contains("session") || changeResponse.Message.Contains("token"))
            {
                return Unauthorized(new { message = changeResponse.Message });
            }

            // For other errors like "Username taken", a 400 Bad Request is suitable.
            return BadRequest(new { message = changeResponse.Message });
        }

        return Ok(changeResponse);
    }
}
