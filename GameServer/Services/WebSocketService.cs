using System.Text.Json;
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
        Console.WriteLine(
            $"[WebSocketService] Step 2: Validating tokens via JwtService. Input: {JsonSerializer.Serialize(request)}"
        );

        // Validate the tokens using the existing robust logic in JwtService
        var authResult = await _jwtService.ValidateOrRefreshAsync(
            request.UserId,
            request.DeviceId,
            request.JwtToken,
            request.RefreshToken
        );

        if (authResult == null)
        {
            Console.WriteLine(
                "[WebSocketService] Token validation failed. Returning unauthenticated."
            );
            return new WebSocketAuthResponse
            {
                Authenticated = false,
                Reason = "Invalid token or session. Please log in again.",
            };
        }

        Console.WriteLine(
            $"[WebSocketService] Token validation successful. AuthResult: {JsonSerializer.Serialize(authResult)}"
        );

        var sessionId = Guid.NewGuid().ToString();

        Console.WriteLine($"[WebSocketService] Generated new SessionId: {sessionId}");

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
            Console.WriteLine(
                $"[WebSocketService] Step 3: Creating new PlayerSessionLog in database for PlayerId: {request.UserId}"
            );
            await _context.PlayerSessionLogs.AddAsync(sessionLog);
            await _context.SaveChangesAsync();
            Console.WriteLine("[WebSocketService] PlayerSessionLog created successfully.");

            var response = new WebSocketAuthResponse
            {
                Authenticated = true,
                SessionId = sessionId,
                Reason = null,
            };

            // If the tokens were refreshed, include the new ones in the response.
            if (authResult.Token != request.JwtToken)
            {
                Console.WriteLine(
                    "[WebSocketService] Tokens were refreshed. Adding new tokens to the response."
                );
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
