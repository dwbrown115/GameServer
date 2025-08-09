using SharedLibrary.Models;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Services;

public class WebSocketService : IWebSocketService
{
    private readonly IJwtService _jwtService;
    private readonly GameDbContext _context;

    public WebSocketService(IJwtService jwtService, GameDbContext context)
    {
        _jwtService = jwtService;
        _context = context;
    }

    public async Task<WebSocketAuthResponse> AuthenticateAsync(WebSocketAuthRequest request)
    {
        // Validate the tokens using the existing robust logic in JwtService
        var authResult = await _jwtService.ValidateOrRefreshAsync(
            request.UserId,
            request.DeviceId,
            request.JwtToken,
            request.RefreshToken
        );

        if (authResult == null)
        {
            return new WebSocketAuthResponse
            {
                Authenticated = false,
                Reason = "Invalid token or session. Please log in again.",
            };
        }

        var sessionId = Guid.NewGuid().ToString();

        var sessionLog = new PlayerSessionLog
        {
            SessionId = sessionId,
            PlayerId = request.UserId,
            DeviceId = request.DeviceId,
            SessionStart = DateTime.UtcNow,
            SessionEnd = null,
            DeletionDate = null,
            ObjectSyncHash = string.Empty,
            SyncStatus = "Initialized",
            DesyncResolution = "None",
            ObjectLifecycleLog = "[]",
            SessionMetadata = "{}",
            Region = "Unknown",
            GameVersion = "Unknown",
            Platform = "Unknown",
            AdminNotes = string.Empty,
        };

        try
        {
            await _context.PlayerSessionLogs.AddAsync(sessionLog);
            await _context.SaveChangesAsync();

            var response = new WebSocketAuthResponse
            {
                Authenticated = true,
                SessionId = sessionId,
                Reason = null,
            };

            // If the tokens were refreshed, include the new ones in the response.
            if (authResult.Token != request.JwtToken)
            {
                response.Token = authResult.Token;
                response.RefreshToken = authResult.RefreshToken;
            }

            return response;
        }
        catch (Exception ex)
        {
            // In a real application, use a structured logger (e.g., ILogger)
            Console.WriteLine($"[ERROR] Failed to create player session log: {ex.Message}");
            return new WebSocketAuthResponse
            {
                Authenticated = false,
                Reason = "An internal error occurred while creating the session.",
            };
        }
    }
}
