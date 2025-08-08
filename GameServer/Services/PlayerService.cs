using Microsoft.EntityFrameworkCore;
using SharedLibrary.Models;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Services;

public interface IPlayerService
{
    Task<PlayerResponse?> GetPlayerAsync(string userId);

    // The method now returns a non-nullable, more descriptive response object
    Task<PlayerChangeResponse> UpdatePlayerDataAsync(PlayerChangeRequest request);
}

public class PlayerService : IPlayerService
{
    private readonly GameDbContext _context;
    private readonly IJwtService _jwtService;

    public PlayerService(GameDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    // ... GetPlayerAsync method remains the same ...
    public async Task<PlayerResponse?> GetPlayerAsync(string userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UUID == userId);

        if (user == null)
            return null;

        return new PlayerResponse { UserId = user.UUID, UserName = user.Username };
    }

    // --- Updated Method Implementation ---
    public async Task<PlayerChangeResponse> UpdatePlayerDataAsync(PlayerChangeRequest request)
    {
        // 1. Validate the refresh token
        var tokenRecord = await _jwtService.GetTokenAsync(request.DeviceId, request.RefreshToken);
        if (tokenRecord == null || tokenRecord.UserId != request.UserId)
        {
            return new PlayerChangeResponse
            {
                Success = false,
                Message = "Invalid session or token.",
            };
        }

        // 2. Find the user to update
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UUID == request.UserId);
        if (user == null)
        {
            // This is an internal inconsistency if the token is valid but user is not found.
            return new PlayerChangeResponse
            {
                Success = false,
                Message = "User associated with token not found.",
            };
        }

        // If no changes are provided in the payload, there's nothing to do.
        if (request.Changes == null)
        {
            return new PlayerChangeResponse
            {
                Success = true,
                Message = "No changes were provided.",
            };
        }

        // 3. Validate and apply the changes from the strongly-typed payload
        var (isValid, errorMessage) = await ValidateAndApplyChangesAsync(user, request.Changes);
        if (!isValid)
        {
            return new PlayerChangeResponse { Success = false, Message = errorMessage };
        }

        // 4. Save the changes to the database
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // 5. Return a success response
        return new PlayerChangeResponse
        {
            Success = true,
            Message = "Player data updated successfully.",
        };
    }

    /// <summary>
    /// Safely validates and applies whitelisted property changes to a User entity using a strongly-typed payload.
    /// </summary>
    private async Task<(bool IsValid, string ErrorMessage)> ValidateAndApplyChangesAsync(
        User user,
        PlayerChangesPayload changes
    )
    {
        // --- Handle Username Change ---
        if (changes.Username != null)
        {
            var newUsername = changes.Username;

            if (string.IsNullOrWhiteSpace(newUsername))
                return (false, "Username cannot be empty.");

            // Only proceed if the new username is actually different from the current one.
            if (!newUsername.Equals(user.Username, StringComparison.OrdinalIgnoreCase))
            {
                // Check if the new username is already taken by another user.
                if (
                    await _context.Users.AnyAsync(u =>
                        u.Username == newUsername && u.UUID != user.UUID
                    )
                )
                {
                    return (false, "Username is already taken.");
                }

                // If all checks pass, update the username.
                user.Username = newUsername;
            }
        }

        // --- Handle Password Change ---
        if (changes.Password != null)
        {
            var oldPassword = changes.Password.OldPassword;
            var newPassword = changes.Password.NewPassword;

            // The [Required] attribute on the model handles nulls, but we validate for empty strings.
            if (string.IsNullOrWhiteSpace(newPassword))
                return (false, "New password cannot be empty.");

            // 1. Verify the user's current password
            if (!AuthenticationHelpers.VerifyPassword(oldPassword, user.PasswordHash, user.Salt))
            {
                return (false, "Incorrect old password.");
            }

            // 2. If verification passes, update the password.
            user.PasswordHash = newPassword;
            user.ProvideSaltAndHash(); // This creates a new salt and hashes the new password.
        }

        return (true, string.Empty);
    }
}
