using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.IdentityModel.Tokens;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IJwtService _jwtService;

    public AuthenticationController(IAuthenticationService authService, IJwtService jwtService)
    {
        _authService = authService;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(AuthenticationRequest request)
    {
        var (success, content) = _authService.Register(request.Username, request.Password);
        if (!success)
            return BadRequest(content);

        return await Login(request);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthenticationRequest request)
    {
        var result = await _authService.Login(request);
        if (result == null)
            return Unauthorized();
        // Console.Write("Response: " + result.Token + " + " + result.RefreshToken + " + " + result.ExpiresAt + " + " + result.UserId);

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        bool success = await _authService.LogoutAsync(request.DeviceId, request.RefreshToken);
        if (!success)
            return NotFound("Token not found or already revoked");

        return Ok(new { message = "Logout successful" });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateToken([FromBody] TokenValidationRequest request)
    {
        var result = await _jwtService.ValidateOrRefreshAsync(
            request.UserId,
            request.DeviceId,
            request.Token,
            request.RefreshToken
        );

        if (result == null)
            return Unauthorized("Token invalid or expired");
        return Ok(result);
    }
}
