using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using GameServer.Models;
using GameServer.Utilities;
using Microsoft.EntityFrameworkCore;
using SharedLibrary;
using SharedLibrary.Responses;

namespace GameServer.Services;

public class JwtValidationResult
{
    public bool IsValid { get; set; }
    public string UserId { get; set; }
    public string DeviceId { get; set; }
    public bool ShouldRefresh { get; set; }
}


public interface IJwtService
{
    JwtValidationResult ValidateEncryptedToken(string token, byte[] secretKey);
    Task<RefreshTokenRecord> GenerateAndStoreJwtAsync(string userId, string deviceId);
    Task<RefreshTokenRecord> GetTokenAsync(string deviceId, string refreshToken);
    Task<RefreshTokenRecord> RefreshTokenAsync(string userId, string deviceId, string refreshToken);
    Task<AuthenticationResponse> ValidateOrRefreshAsync(string userId, string deviceId, string token, string refreshToken);
    string DecryptRefreshToken(string encrypted, byte[] key);
    string GenerateJwt(string userId, byte[] secretKey);
}



public class JwtService : IJwtService
{
    private readonly Settings _settings;
    private readonly GameDbContext _context;

    public JwtService(Settings settings, GameDbContext context)
    {
        _settings = settings;
        _context = context;
    }

    public string GenerateJwt(string userId, byte[] secretKey)
    {
        var expires = DateTime.UtcNow.AddMinutes(30);

        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", userId) }),
            Expires = expires,
            EncryptingCredentials = new EncryptingCredentials(
                new SymmetricSecurityKey(secretKey),
                SecurityAlgorithms.Aes256KW,
                SecurityAlgorithms.Aes256CbcHmacSha512
            )
        };

        var token = handler.CreateJwtSecurityToken(descriptor);
        return handler.WriteToken(token);
    }

    public JwtValidationResult ValidateEncryptedToken(string token, byte[] secretKey)
    {
        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            TokenDecryptionKey = new SymmetricSecurityKey(secretKey),
            ValidateLifetime = true,
            ValidateIssuer = false,
            ValidateAudience = false
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParams, out var validatedToken);
            var userId = principal.FindFirst("sub")?.Value;
            return new JwtValidationResult
            {
                IsValid = true,
                UserId = userId,
                ShouldRefresh = ((validatedToken.ValidTo - DateTime.UtcNow).TotalMinutes < 10)
            };
        }
        catch
        {
            return new JwtValidationResult { IsValid = false };
        }
    }

    public async Task<RefreshTokenRecord> GenerateAndStoreJwtAsync(string userId, string deviceId)
    {
        var secretKey = EncryptionUtility.GenerateEncryptionKey();
        var refreshToken = EncryptionUtility.GenerateSecureToken();
        var encryptedRefreshToken = EncryptionUtility.Encrypt(refreshToken, secretKey);
        var jwt = GenerateJwt(userId, secretKey);

        var existing = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.UserId == userId && r.DeviceId == deviceId);
        if (existing != null)
        {
            existing.EncryptedRefreshToken = encryptedRefreshToken;
            existing.SecretKey = secretKey;
            existing.ExpiresAt = DateTime.UtcNow.AddDays(14);
            existing.IsRevoked = false;
            
            // Console.WriteLine("Encrypted RefreshToken: " + encryptedRefreshToken + " + " + secretKey);


            _context.RefreshTokens.Update(existing);
        }
        else
        {
            var record = new RefreshTokenRecord
            {
                UserId = userId,
                DeviceId = deviceId,
                EncryptedRefreshToken = encryptedRefreshToken,
                SecretKey = secretKey,
                ExpiresAt = DateTime.UtcNow.AddDays(14),
                IsRevoked = false
            };

            await _context.RefreshTokens.AddAsync(record);
        }

        await _context.SaveChangesAsync();

        // Return plain refresh token (not encrypted) for client use
        return new RefreshTokenRecord
        {
            UserId = userId,
            DeviceId = deviceId,
            EncryptedRefreshToken = refreshToken,
            SecretKey = secretKey,
            ExpiresAt = DateTime.UtcNow.AddDays(14)
        };
    }

    public async Task<RefreshTokenRecord> GetTokenAsync(string deviceId, string refreshToken)
    {
        var record = await _context.RefreshTokens.FirstOrDefaultAsync(t =>
            t.DeviceId == deviceId && !t.IsRevoked &&
            DecryptRefreshToken(t.EncryptedRefreshToken, t.SecretKey) == refreshToken);

        return record;
    }

    public async Task<RefreshTokenRecord> RefreshTokenAsync(string userId, string deviceId, string refreshToken)
    {
        var record = await GetTokenAsync(deviceId, refreshToken);
        if (record is null || record.ExpiresAt < DateTime.UtcNow)
            return null;

        record.IsRevoked = true;
        _context.RefreshTokens.Update(record);

        var newRecord = await GenerateAndStoreJwtAsync(userId, deviceId);
        await _context.SaveChangesAsync();
        return newRecord;
    }

    public async Task<AuthenticationResponse> ValidateOrRefreshAsync(string userId, string deviceId, string token, string refreshToken)
    {
        // Console.Write("ValidateOrRefreshAsync: " + userId + " + " +  deviceId + " + " + token + " + " + refreshToken);
        // Pull eligible records (still filtered by SQL!)
        var candidates = await _context.RefreshTokens
            .Where(r => r.DeviceId == deviceId && !r.IsRevoked)
            .ToListAsync();
        Console.WriteLine("🔍 Candidate Tokens:");
        foreach (var c in candidates)
        {
            // Console.WriteLine($"— Token ID: {c.Id}, DeviceId: {c.DeviceId}, IsRevoked: {c.IsRevoked}, Encrypted: {c.EncryptedRefreshToken}, Expires: {c.ExpiresAt}");
            Console.WriteLine(DecryptRefreshToken("Decrypted Key" + c.EncryptedRefreshToken, c.SecretKey));
        }

        // Run decryption securely in memory
        var record = candidates.FirstOrDefault(r =>
            DecryptRefreshToken(r.EncryptedRefreshToken, r.SecretKey) == refreshToken); 
        // Console.WriteLine("Record: " + record);
        //
        if (record == null || record.IsRevoked || record.ExpiresAt < DateTime.UtcNow)
            return null;
        
        // if (record != null)
        // {
        //     Console.WriteLine("✅ Matching Record Found:");
        //     Console.WriteLine($"Token ID: {record.Id}");
        //     Console.WriteLine($"DeviceId: {record.DeviceId}");
        //     Console.WriteLine($"Expires At: {record.ExpiresAt}");
        //     Console.WriteLine($"Encrypted RefreshToken: {record.EncryptedRefreshToken}");
        // }
        // else
        // {
        //     Console.WriteLine("❌ No matching refresh token found after decryption.");
        // }
        
        var validation = ValidateEncryptedToken(token, record.SecretKey);
        Console.WriteLine("Validation: " + validation);

        if (!validation.IsValid || validation.ShouldRefresh)
        {
            record.IsRevoked = true;
            _context.RefreshTokens.Update(record);

            var newToken = await GenerateAndStoreJwtAsync(userId, deviceId);

            return new AuthenticationResponse
            {
                Token = GenerateJwt(userId, newToken.SecretKey),
                RefreshToken = newToken.EncryptedRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };
        }

        return new AuthenticationResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }

    public string DecryptRefreshToken(string encrypted, byte[] key)
    {
        try
        {
            byte[] fullCipher = Convert.FromBase64String(encrypted);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Padding = PaddingMode.PKCS7;

            byte[] iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"❌ Decryption failed: {ex.Message}");
            return null;
        }
    }


}


