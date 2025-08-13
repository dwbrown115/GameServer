using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GameServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SharedLibrary.Models;
using SharedLibrary.Responses;
using SharedLibrary.Results;

namespace GameServer.Services;

public interface IJwtService
{
    JwtValidationResult ValidateSignedToken(string token);
    Task<RefreshTokenRecord> GenerateAndStoreJwtAsync(string userId, string deviceId);
    Task<RefreshTokenRecord?> GetTokenAsync(string deviceId, string refreshToken);
    Task<RefreshTokenRecord?> RefreshTokenAsync(
        string userId,
        string deviceId,
        string refreshToken
    );
    Task<AuthenticationResponse?> ValidateOrRefreshAsync(
        string userId,
        string deviceId,
        string token,
        string refreshToken
    );
    string GenerateJwt(string userId);
}

public class JwtService : IJwtService
{
    private readonly Settings _settings;
    private readonly GameDbContext _context;
    private readonly byte[] _jwtSecretBytes;

    public JwtService(Settings settings, GameDbContext context)
    {
        _settings = settings;
        _context = context;
        _jwtSecretBytes = System.Text.Encoding.ASCII.GetBytes(_settings.JwtSecret);
    }

    public string GenerateJwt(string userId)
    {
        var expires = DateTime.UtcNow.AddMinutes(30);

        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", userId) }),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_jwtSecretBytes),
                SecurityAlgorithms.HmacSha256
            ),
        };

        var token = handler.CreateJwtSecurityToken(descriptor);
        return handler.WriteToken(token);
    }

    public JwtValidationResult ValidateSignedToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(_jwtSecretBytes),
            ValidateLifetime = true,
            ValidateIssuer = false,
            ValidateAudience = false,
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParams, out var validatedToken);
            var userId = principal.FindFirst("sub")?.Value;
            return new JwtValidationResult
            {
                IsValid = true,
                UserId = userId,
                ShouldRefresh = ((validatedToken.ValidTo - DateTime.UtcNow).TotalMinutes < 10),
            };
        }
        catch
        {
            return new JwtValidationResult { IsValid = false };
        }
    }

    public async Task<RefreshTokenRecord> GenerateAndStoreJwtAsync(string userId, string deviceId)
    {
        var refreshToken = Guid.NewGuid().ToString("N");

        var existing = await _context.RefreshTokens.FirstOrDefaultAsync(r =>
            r.UserId == userId && r.DeviceId == deviceId
        );
        if (existing != null)
        {
            existing.EncryptedRefreshToken = refreshToken; // Store plain text
            existing.ExpiresAt = DateTime.UtcNow.AddDays(14);
            existing.IsRevoked = false;

            _context.RefreshTokens.Update(existing);
        }
        else
        {
            var record = new RefreshTokenRecord
            {
                UserId = userId,
                DeviceId = deviceId,
                EncryptedRefreshToken = refreshToken, // Store plain text
                ExpiresAt = DateTime.UtcNow.AddDays(14),
                IsRevoked = false,
            };

            await _context.RefreshTokens.AddAsync(record);
        }

        await _context.SaveChangesAsync();

        // Return plain refresh token for client use
        return new RefreshTokenRecord
        {
            UserId = userId,
            DeviceId = deviceId,
            EncryptedRefreshToken = refreshToken, // This is the plain text token for the client
            ExpiresAt = DateTime.UtcNow.AddDays(14),
        };
    }

    public async Task<RefreshTokenRecord?> GetTokenAsync(string deviceId, string refreshToken)
    {
        // Directly compare the refresh token as it's now stored in plain text
        var record = await _context
            .RefreshTokens.FirstOrDefaultAsync(t =>
                t.DeviceId == deviceId && !t.IsRevoked && t.EncryptedRefreshToken == refreshToken
            );

        return record;
    }

    public async Task<RefreshTokenRecord?> RefreshTokenAsync(
        string userId,
        string deviceId,
        string refreshToken
    )
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

    public async Task<AuthenticationResponse?> ValidateOrRefreshAsync(
        string userId,
        string deviceId,
        string token,
        string refreshToken
    )
    {
        // Console.WriteLine(
        //     $"[JwtService] ValidateOrRefreshAsync called. UserId: {userId}, DeviceId: {deviceId}"
        // );

        // 1. Pull eligible, NON-REVOKED records from the database.
        var record = await _context
            .RefreshTokens.FirstOrDefaultAsync(r =>
                r.DeviceId == deviceId && !r.IsRevoked && r.EncryptedRefreshToken == refreshToken
            );

        // 3. If no matching token is found, or if it has expired, it's invalid.
        if (record == null || record.ExpiresAt < DateTime.UtcNow)
        {
            // Console.WriteLine(
            //     $"[JwtService] No valid refresh token record found or it has expired. Expiry: {record?.ExpiresAt}"
            // );
            return null;
        }

        Console.WriteLine($"[JwtService] Found matching refresh token record. Id: {record.Id}");
        Console.WriteLine($"[JwtService] Refresh Token from DB: {record.EncryptedRefreshToken}, Client Token: {refreshToken}");

        var validation = ValidateSignedToken(token);

        // Console.WriteLine(
        //     $"[JwtService] JWT validation result: IsValid={validation.IsValid}, ShouldRefresh={validation.ShouldRefresh}"
        // );

        // 4. If the main JWT is invalid or needs to be refreshed
        if (!validation.IsValid || validation.ShouldRefresh)
        {
            // Console.WriteLine(
            //     "/[JwtService] JWT is invalid or needs refresh. Generating new token pair."
            // );

            // Revoke the old refresh token
            record.IsRevoked = true;
            _context.RefreshTokens.Update(record);

            // Generate a new token pair
            var newToken = await GenerateAndStoreJwtAsync(userId, deviceId);
            await _context.SaveChangesAsync(); // Save the revocation and the new token

            var newAuthResponse = new AuthenticationResponse
            {
                UserId = userId,
                Token = GenerateJwt(userId),
                RefreshToken = newToken.EncryptedRefreshToken, // This is the plain text token for the client
                ExpiresAt = DateTime.UtcNow.AddMinutes(30), // Expiry of the new JWT
            };

            // Console.WriteLine(
            //     $"[JwtService] New token pair generated and returned. New JWT: {newAuthResponse.Token.Substring(0, 15)}..."
            // );

            return newAuthResponse;
        }

        // 5. If the current token is still valid, return it with its correct expiry.
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Console.WriteLine(
        //     "[JwtService] Current JWT is valid and does not need refresh. Returning existing tokens."
        // );

        return new AuthenticationResponse
        {
            UserId = userId,
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = jwtToken.ValidTo, // Use the actual expiry from the token
        };
    }
}