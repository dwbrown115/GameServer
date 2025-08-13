using System.Security.Claims;
using System.Security.Cryptography;
using GameServer.Models;
using GameServer.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Models;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly Settings _settings;
    private readonly GameDbContext _context;
    private readonly IJwtService _jwtService;

    public AuthenticationService(Settings settings, GameDbContext context, IJwtService jwtService)
    {
        _settings = settings;
        _context = context;
        _jwtService = jwtService;
    }

    public (bool success, string content) Register(string username, string password)
    {
        if (_context.Users.Any(u => u.Username == username))
            return (false, "Username not available");

        var (salt, hash) = AuthenticationHelpers.GenerateSaltAndHash(password);

        var user = new User
        {
            Username = username,
            PasswordHash = hash,
            UUID = UserIdUtility.GenerateBase64UserId(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Salt = salt,
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        // Console.WriteLine($"[AuthenticationService] User '{username}' registered successfully with UUID: {user.UUID}");
        return (true, "");
    }

    public async Task<LoginResult?> Login(AuthenticationRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !AuthenticationHelpers.VerifyPassword(request.Password, user.PasswordHash, user.Salt))
            return null;

        var tokenRecord = await _jwtService.GenerateAndStoreJwtAsync(user.UUID, request.DeviceId);
        var jwt = _jwtService.GenerateJwt(user.UUID);

        var loginResult = new LoginResult
        {
            UserId = user.UUID,
            Token = jwt,
            RefreshToken = tokenRecord.EncryptedRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
        };

        // Console.WriteLine($"[AuthenticationService] User '{user.Username}' logged in successfully on device: {request.DeviceId}");
        return loginResult;
    }

    public async Task<bool> LogoutAsync(string deviceId, string refreshToken)
    {
        // Do not log the refresh token itself for security.
        // Console.WriteLine($"[AuthenticationService] Attempting logout for DeviceId: {deviceId}");
        var record = await _jwtService.GetTokenAsync(deviceId, refreshToken);
        if (record == null || record.DeviceId != deviceId)
            return false;

        record.IsRevoked = true;
        _context.RefreshTokens.Update(record);
        await _context.SaveChangesAsync();
        // Console.WriteLine($"[AuthenticationService] Token for DeviceId '{deviceId}' revoked successfully.");
        return true;
    }

    public IActionResult UnauthorizedResponse(string reason = "Unauthorized access")
    {
        return new UnauthorizedObjectResult(
            new
            {
                status = 401,
                error = reason,
                timestamp = DateTime.UtcNow,
            }
        );
    }

    private ClaimsIdentity AssembleClaimsIdentity(User user)
    {
        return new ClaimsIdentity(
            new[]
            {
                new Claim("id", user.Id.ToString()),
                // Additional claims can be added here
            }
        );
    }
}

public interface IAuthenticationService
{
    (bool success, string content) Register(string username, string password);
    Task<LoginResult?> Login(AuthenticationRequest request);
    Task<bool> LogoutAsync(string deviceId, string refreshToken);
}

public static class AuthenticationHelpers
{
    public static (string salt, string hash) GenerateSaltAndHash(string password)
    {
        var saltBytes = GenerateSalt();
        var salt = Convert.ToBase64String(saltBytes);
        var hash = ComputeHash(password, salt);
        return (salt, hash);
    }

    private static byte[] GenerateSalt()
    {
        var rng = RandomNumberGenerator.Create();
        var salt = new byte[24];
        rng.GetBytes(salt);
        return salt;
    }

    public static string ComputeHash(string password, string saltString)
    {
        var salt = Convert.FromBase64String(saltString);

        using var hashGenerator = new Rfc2898DeriveBytes(
            password,
            salt,
            10101,
            HashAlgorithmName.SHA256
        );
        var bytes = hashGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes);
    }

    // --- NEW PUBLIC HELPER METHOD ---
    /// <summary>
    /// Verifies a password against a stored hash and salt.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        return storedHash == ComputeHash(password, storedSalt);
    }
}
