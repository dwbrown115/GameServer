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
        // Add the new user
        _context.Users.Add(user);

        // Automatically provision a gameplay.UserData row if one does not already exist for this user (should not in normal registration path)
        if (!_context.UserDatas.Any(ud => ud.UserId == user.UUID))
        {
            // Attempt to auto-grant the white skin (hex #FFFFFF) if it exists
            string whiteSkinHex = "#FFFFFF";
            var whiteSkin = _context.Skins.FirstOrDefault(s => s.HexValue == whiteSkinHex);
            var ownedList = new List<object>();
            if (whiteSkin != null)
            {
                ownedList.Add(new { SkinId = whiteSkin.UUID });
            }
            var userData = new UserData
            {
                UserId = user.UUID,
                Points = 0,
                OwnedSkins = Newtonsoft.Json.JsonConvert.SerializeObject(ownedList),
                PointsLog = Newtonsoft.Json.JsonConvert.SerializeObject(new List<PointsLogEntry>()),
                ActiveSkin = whiteSkin != null ? whiteSkin.UUID : "#FFFFFF", // prefer actual skin uuid if present
            };
            _context.UserDatas.Add(userData);
        }

        _context.SaveChanges();

        // Console.WriteLine($"[AuthenticationService] User '{username}' registered successfully with UUID: {user.UUID}");
        return (true, "");
    }

    public async Task<LoginResult?> Login(AuthenticationRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (
            user == null
            || !AuthenticationHelpers.VerifyPassword(request.Password, user.PasswordHash, user.Salt)
        )
        {
            return null;
        }

        // Ensure UserData exists (should normally from registration) & perform white skin backfill if needed.
        var userData = await _context.UserDatas.FirstOrDefaultAsync(ud => ud.UserId == user.UUID);
        if (userData == null)
        {
            // Mirror registration provisioning (white skin seeding attempted)
            string whiteSkinHex = "#FFFFFF";
            var whiteSkin = await _context.Skins.FirstOrDefaultAsync(s =>
                s.HexValue == whiteSkinHex
            );
            var ownedList = new List<object>();
            string activeSkinValue = whiteSkinHex;
            if (whiteSkin != null)
            {
                ownedList.Add(new { SkinId = whiteSkin.UUID });
                activeSkinValue = whiteSkin.UUID;
            }
            userData = new SharedLibrary.Models.UserData
            {
                UserId = user.UUID,
                Points = 0,
                OwnedSkins = Newtonsoft.Json.JsonConvert.SerializeObject(ownedList),
                PointsLog = Newtonsoft.Json.JsonConvert.SerializeObject(new List<PointsLogEntry>()),
                ActiveSkin = activeSkinValue,
            };
            _context.UserDatas.Add(userData);
            await _context.SaveChangesAsync();
        }
        else
        {
            bool ownedEmpty =
                string.IsNullOrWhiteSpace(userData.OwnedSkins)
                || userData.OwnedSkins.Trim() == "[]";
            bool activeBlankOrFallback =
                string.IsNullOrWhiteSpace(userData.ActiveSkin) || userData.ActiveSkin == "#FFFFFF";
            if (ownedEmpty || activeBlankOrFallback)
            {
                var whiteSkin = await _context.Skins.FirstOrDefaultAsync(s =>
                    s.HexValue == "#FFFFFF"
                );
                if (whiteSkin != null)
                {
                    // Deserialize existing list (if any) to avoid wiping other skins
                    List<TempOwnership> existingOwned = new();
                    if (!string.IsNullOrWhiteSpace(userData.OwnedSkins))
                    {
                        try
                        {
                            existingOwned =
                                Newtonsoft.Json.JsonConvert.DeserializeObject<List<TempOwnership>>(
                                    userData.OwnedSkins!
                                ) ?? new List<TempOwnership>();
                        }
                        catch
                        {
                            existingOwned = new List<TempOwnership>();
                        }
                    }
                    if (!existingOwned.Any(e => e.SkinId == whiteSkin.UUID))
                    {
                        existingOwned.Add(new TempOwnership { SkinId = whiteSkin.UUID });
                        userData.OwnedSkins = Newtonsoft.Json.JsonConvert.SerializeObject(
                            existingOwned
                        );
                    }
                    if (activeBlankOrFallback)
                    {
                        userData.ActiveSkin = whiteSkin.UUID;
                    }
                    await _context.SaveChangesAsync();
                }
            }
        }

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

public class TempOwnership
{
    public string SkinId { get; set; } = string.Empty;
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
