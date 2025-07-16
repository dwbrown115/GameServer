using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using GameServer.Models;
using GameServer.Utilities;
using Microsoft.AspNetCore.Mvc;
using SharedLibrary;
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

        var user = new User
        {
            Username = username,
            PasswordHash = password,
            UUID = UserIdUtility.GenerateBase64UserId(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.ProvideSaltAndHash();
        _context.Users.Add(user);
        _context.SaveChanges();

        return (true, "");
    }

    public async Task<LoginResult> Login(AuthenticationRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash, user.Salt))
            return null;
        
        // Console.WriteLine("device id: " + request.DeviceId);

        var tokenRecord = await _jwtService.GenerateAndStoreJwtAsync(user.UUID, request.DeviceId);
        // Console.WriteLine("Token Record: "  + tokenRecord.Id);
        var jwt = _jwtService.GenerateJwt(user.UUID, tokenRecord.SecretKey);
        // Console.WriteLine("JWT Token: " + jwt);

        return new LoginResult
        {
            UserId = user.UUID,
            Token = jwt,
            RefreshToken = tokenRecord.EncryptedRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }

    public async Task<bool> LogoutAsync(string userId, string deviceId, string refreshToken)
    {
        var record = await _jwtService.GetTokenAsync(deviceId, refreshToken);
        if (record == null || record.UserId != userId)
            return false;

        record.IsRevoked = true;
        _context.RefreshTokens.Update(record);
        await _context.SaveChangesAsync();
        return true;
    }

    private bool VerifyPassword(string password, string passwordHash, string passwordSalt)
    {
        return passwordHash == AuthenticationHelpers.ComputeHash(password, passwordSalt);
    }

    public IActionResult UnauthorizedResponse(string reason = "Unauthorized access")
    {
        return new UnauthorizedObjectResult(new
        {
            status = 401,
            error = reason,
            timestamp = DateTime.UtcNow
        });
    }

    private ClaimsIdentity AssembleClaimsIdentity(User user)
    {
        return new ClaimsIdentity(new[] {
            new Claim("id", user.Id.ToString())
            // Additional claims can be added here
        });
    }
}


public interface IAuthenticationService
{
    (bool success, string content) Register(string username, string password);
    Task<LoginResult> Login(AuthenticationRequest request);
    Task<bool> LogoutAsync(string userId, string deviceId, string refreshToken);
}



public static class AuthenticationHelpers {
    public static void ProvideSaltAndHash(this User user) {
        var salt = GenerateSalt();
        user.Salt = Convert.ToBase64String(salt);
        user.PasswordHash = ComputeHash(user.PasswordHash, user.Salt);
    }

    private static byte[] GenerateSalt() {
        var rng = RandomNumberGenerator.Create();
        var salt = new byte[24];
        rng.GetBytes(salt);
        return salt;
    }

    public static string ComputeHash(string password, string saltString) {
        var salt = Convert.FromBase64String(saltString);

        using var hashGenerator = new Rfc2898DeriveBytes(password, salt);
        hashGenerator.IterationCount = 10101;
        var bytes = hashGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes);
    }
    
    
}


// public async Task<IActionResult> Login(AuthenticationRequest request) {
//     var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
//     if (user == null || !VerifyPassword(request.Password, user.PasswordHash, user.Salt)) return UnauthorizedResponse();
//
//     var jwt = await _jwtService.GenerateAndStoreJwtAsync(user.UUID);
//
//     // return Results.Ok(new
//     // {
//     //     token = jwt,
//     //     refreshToken = jwt.RefreshToken,
//     //     username = user.Username,
//     // });
//     // var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
//     // if (user == null) return (false, "Invalid username");
//
//     // if (user.PasswordHash != AuthenticationHelpers.ComputeHash(password, user.Salt)) return (false, "Invalid password");
//     // Console.WriteLine($"Logging in {username}");
//     // return (true, GenerateJwtToken(AssembleClaimsIdentity(user)));
//
//     
//     // return OkResponse({new token = jwt.})
//
// }
// private string GenerateJwtToken(ClaimsIdentity subject, string uuid) {
//     Console.WriteLine("UUID: " + uuid);
//     var tokenHandler = new JwtSecurityTokenHandler();
//     var key = Encoding.ASCII.GetBytes(_settings.BearerKey);
//     var tokenDescriptor = new SecurityTokenDescriptor {
//         Subject = subject,
//         Expires = DateTime.Now.AddDays(14),
//         SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
//     };
//     var token = tokenHandler.CreateToken(tokenDescriptor);
//         
//     // var JwtToken = new JwtToken {}
//     return tokenHandler.WriteToken(token);
// }