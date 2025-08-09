using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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
        // Log the request without the password for security.
        Console.WriteLine($"[AuthenticationController] Received register request: {JsonConvert.SerializeObject(new { request.Username, request.DeviceId })}");
        var (success, content) = _authService.Register(request.Username, request.Password);
        if (!success)
        {
            Console.WriteLine($"[AuthenticationController] Registration failed for {request.Username}: {content}");
            return BadRequest(new { message = content });
        }

        Console.WriteLine($"[AuthenticationController] Registration successful for {request.Username}. Proceeding to login.");
        return await Login(request);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthenticationRequest request)
    {
        Console.WriteLine($"[AuthenticationController] Received login request: {JsonConvert.SerializeObject(new { request.Username, request.DeviceId })}");
        var result = await _authService.Login(request);
        if (result == null)
        {
            Console.WriteLine($"[AuthenticationController] Login failed for user: {request.Username}");
            return Unauthorized(new { message = "Invalid username or password." });
        }

        Console.WriteLine($"[AuthenticationController] Login successful for user: {result.UserId}");
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        Console.WriteLine($"[AuthenticationController] Received logout request: {JsonConvert.SerializeObject(request)}");
        bool success = await _authService.LogoutAsync(request.DeviceId, request.RefreshToken);
        if (!success)
        {
            Console.WriteLine($"[AuthenticationController] Logout failed for device: {request.DeviceId}");
            return NotFound(new { message = "Token not found or already revoked" });
        }

        Console.WriteLine($"[AuthenticationController] Logout successful for device: {request.DeviceId}");
        return Ok(new { message = "Logout successful" });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateToken([FromBody] TokenValidationRequest request)
    {
        Console.WriteLine($"[AuthenticationController] Received token validation request for user: {request.UserId}");
        var result = await _jwtService.ValidateOrRefreshAsync(
            request.UserId,
            request.DeviceId,
            request.Token,
            request.RefreshToken
        );

        if (result == null)
        {
            Console.WriteLine($"[AuthenticationController] Token validation failed for user: {request.UserId}");
            return Unauthorized(new { message = "Token invalid or expired" });
        }

        bool refreshed = result.Token != request.Token;
        Console.WriteLine($"[AuthenticationController] Token validation successful for user: {request.UserId}. Refreshed: {refreshed}");
        return Ok(result);
    }
}
