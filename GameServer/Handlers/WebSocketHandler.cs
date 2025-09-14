using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using GameServer.GameModules.Abstractions;
using GameServer.Models;
using GameServer.Services;
using GameServer.Utilities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLibrary.Models;
using SharedLibrary.Modules.AgarSurvivor.Models;
using SharedLibrary.Modules.AgarSurvivor.Requests;
using SharedLibrary.Modules.AgarSurvivor.Responses;

namespace GameServer.Handlers;

public class ClaimObjectWebSocketRequest
{
    [JsonProperty("request_type")]
    public string RequestType { get; set; } = null!;

    [JsonProperty("session_id")]
    public string SessionId { get; set; } = null!;

    [JsonProperty("claimedObject")]
    public ObjectClaimedRequest ClaimedObject { get; set; } = null!;
}

public class WebSocketHandler : IWebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IWebSocketConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Settings _settings;
    private readonly IWebSocketMessageRegistry _messageRegistry;

    public WebSocketHandler(
        ILogger<WebSocketHandler> logger,
        IWebSocketConnectionManager connectionManager,
        IServiceScopeFactory scopeFactory,
        Settings settings,
        IWebSocketMessageRegistry messageRegistry
    )
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _scopeFactory = scopeFactory;
        _settings = settings;
        _messageRegistry = messageRegistry;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var sessionId = context.Request.Query["sessionId"].FirstOrDefault();
        if (string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("SessionId is required.");
            return;
        }

        // Use a service scope to resolve scoped services like DbContext
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var sessionLog = await dbContext.PlayerSessionLogs.FirstOrDefaultAsync(s =>
            s.SessionId == sessionId && s.SessionEnd == null
        );

        if (sessionLog == null)
        {
            _logger.LogWarning(
                "WebSocket connection rejected for invalid or expired SessionId: {SessionId}",
                sessionId
            );
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid or expired session.");
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        _connectionManager.AddSocket(sessionId, socket);
        _logger.LogInformation(
            "WebSocket connection established for PlayerId: {PlayerId} with SessionId: {SessionId}",
            sessionLog.PlayerId,
            sessionId
        );

        try
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult receiveResult;

            do
            {
                receiveResult = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None
                );

                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    var messageString = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);

                    try
                    {
                        var jObject = JObject.Parse(messageString);
                        var requestType = jObject["request_type"]?.Value<string>();

                        if (_messageRegistry.TryGetHandler(requestType, out var handler))
                        {
                            await handler.HandleAsync(jObject, socket, dbContext, sessionLog);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Received unhandled message with request_type {RequestType}: {Message}",
                                requestType,
                                messageString
                            );
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning(
                            "Could not deserialize message from SessionId {SessionId}. Error: {Error}. Message: {Message}",
                            sessionId,
                            jsonEx.Message,
                            messageString
                        );
                    }
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation(
                        "Client initiated WebSocket close. Status: {Status}, Description: {Description}",
                        receiveResult.CloseStatus,
                        receiveResult.CloseStatusDescription
                    );
                    await socket.CloseAsync(
                        receiveResult.CloseStatus!.Value,
                        receiveResult.CloseStatusDescription,
                        CancellationToken.None
                    );
                }
            } while (!receiveResult.CloseStatus.HasValue);
        }
        catch (WebSocketException ex)
        {
            _logger.LogInformation(
                "WebSocket connection closed for SessionId {SessionId}. Reason: {Message}",
                sessionId,
                ex.Message
            );
        }
        finally
        {
            // Mark session end timestamp if not already set
            if (sessionLog != null && sessionLog.SessionEnd == null)
            {
                try
                {
                    sessionLog.SessionEnd = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation(
                        "Session {SessionId} ended at {SessionEndUtc}",
                        sessionId,
                        sessionLog.SessionEnd
                    );
                }
                catch (Exception persistEx)
                {
                    _logger.LogError(
                        persistEx,
                        "Failed to persist session end for SessionId {SessionId}",
                        sessionId
                    );
                }
            }
            // Upsert into gameplay.Leaderboard based on this session's final server score
            if (sessionLog != null)
            {
                try
                {
                    var userId = sessionLog.PlayerId; // PlayerId holds the UserId for the session
                    var currentScore = sessionLog.ScoreServer;
                    var now = DateTime.UtcNow;

                    // Fetch existing leaderboard entry
                    var lbEntry = await dbContext.Leaderboards.FirstOrDefaultAsync(l =>
                        l.UserId == userId
                    );
                    if (lbEntry == null)
                    {
                        // Resolve username from users.Users table
                        var userRecord = await dbContext.Users.FirstOrDefaultAsync(u =>
                            u.UUID == userId
                        );
                        var username = userRecord?.Username ?? "Unknown";
                        lbEntry = new Leaderboard
                        {
                            UserId = userId,
                            Username = username,
                            PlayerHighestScore = currentScore,
                            PreviousScoreTimestamp = now,
                            ScoreTimestamp = now,
                            HighScoreLog = JsonConvert.SerializeObject(
                                new List<HighScoreLogEntry>
                                {
                                    new HighScoreLogEntry
                                    {
                                        HighScoreAtTime = currentScore,
                                        HighScoreAtTimestamp = now,
                                    },
                                }
                            ),
                        };
                        await dbContext.Leaderboards.AddAsync(lbEntry);
                        _logger.LogInformation(
                            "Leaderboard entry created for UserId {UserId} with score {Score}",
                            userId,
                            currentScore
                        );
                    }
                    else
                    {
                        // Always overwrite score and shift timestamps per updated requirement
                        lbEntry.PreviousScoreTimestamp = lbEntry.ScoreTimestamp;
                        lbEntry.ScoreTimestamp = now;
                        lbEntry.PlayerHighestScore = currentScore;
                        var userRecord = await dbContext.Users.FirstOrDefaultAsync(u =>
                            u.UUID == userId
                        );
                        if (userRecord != null && userRecord.Username != lbEntry.Username)
                            lbEntry.Username = userRecord.Username;

                        // Initialize or append to HighScoreLog
                        try
                        {
                            List<HighScoreLogEntry> logEntries;
                            if (string.IsNullOrWhiteSpace(lbEntry.HighScoreLog))
                            {
                                logEntries = new List<HighScoreLogEntry>();
                            }
                            else
                            {
                                logEntries =
                                    JsonConvert.DeserializeObject<List<HighScoreLogEntry>>(
                                        lbEntry.HighScoreLog!
                                    ) ?? new List<HighScoreLogEntry>();
                            }
                            logEntries.Add(
                                new HighScoreLogEntry
                                {
                                    HighScoreAtTime = currentScore,
                                    HighScoreAtTimestamp = now,
                                }
                            );
                            lbEntry.HighScoreLog = JsonConvert.SerializeObject(logEntries);
                        }
                        catch (Exception parseEx)
                        {
                            _logger.LogWarning(
                                parseEx,
                                "Failed to parse existing HighScoreLog for UserId {UserId}; reinitializing.",
                                userId
                            );
                            lbEntry.HighScoreLog = JsonConvert.SerializeObject(
                                new List<HighScoreLogEntry>
                                {
                                    new HighScoreLogEntry
                                    {
                                        HighScoreAtTime = currentScore,
                                        HighScoreAtTimestamp = now,
                                    },
                                }
                            );
                        }
                        _logger.LogInformation(
                            "Leaderboard entry refreshed for UserId {UserId} -> Score {Score} (prev ts {PrevTs} new ts {NewTs})",
                            userId,
                            currentScore,
                            lbEntry.PreviousScoreTimestamp,
                            lbEntry.ScoreTimestamp
                        );
                    }
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception lbEx)
                {
                    _logger.LogError(
                        lbEx,
                        "Failed to upsert leaderboard for SessionId {SessionId} / UserId {UserId}",
                        sessionId,
                        sessionLog.PlayerId
                    );
                }
            }
            // Upsert / award points in gameplay.UserData
            if (sessionLog != null)
            {
                try
                {
                    var userId = sessionLog.PlayerId;
                    var sessionPoints = sessionLog.ScoreServer;
                    var userData = await dbContext.UserDatas.FirstOrDefaultAsync(u =>
                        u.UserId == userId
                    );
                    // UserData creation & white skin seeding now handled during login/token validation.
                    if (userData == null)
                    {
                        var userExists = await dbContext.Users.AnyAsync(u => u.UUID == userId);
                        if (!userExists)
                        {
                            _logger.LogWarning(
                                "Skipping UserData creation; user {UserId} not found in users.Users",
                                userId
                            );
                        }
                        else
                        {
                            userData = new UserData
                            {
                                UserId = userId,
                                Points = 0,
                                OwnedSkins = JsonConvert.SerializeObject(new List<object>()),
                                PointsLog = JsonConvert.SerializeObject(
                                    new List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry>()
                                ),
                                ActiveSkin = "#FFFFFF",
                            };
                            await dbContext.UserDatas.AddAsync(userData);
                            await dbContext.SaveChangesAsync();
                            _logger.LogInformation(
                                "Initialized bare UserData for UserId {UserId}",
                                userId
                            );
                        }
                    }
                    if (userData != null)
                    {
                        // Parse or init PointsLog
                        List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry> pointsLog;
                        if (string.IsNullOrWhiteSpace(userData.PointsLog))
                        {
                            pointsLog =
                                new List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry>();
                        }
                        else
                        {
                            try
                            {
                                pointsLog =
                                    JsonConvert.DeserializeObject<
                                        List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry>
                                    >(userData.PointsLog!)
                                    ?? new List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry>();
                            }
                            catch
                            {
                                pointsLog =
                                    new List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry>();
                            }
                        }
                        // Snapshot before update
                        pointsLog.Add(
                            new SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry
                            {
                                PointsAtTime = userData.Points,
                                PointsAtTimestamp = DateTime.UtcNow,
                            }
                        );
                        userData.PointsLog = JsonConvert.SerializeObject(pointsLog);
                        userData.Points += sessionPoints;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation(
                            "Awarded {Points} points to UserId {UserId}. New total {Total}",
                            sessionPoints,
                            userId,
                            userData.Points
                        );
                    }
                }
                catch (Exception userDataEx)
                {
                    _logger.LogError(
                        userDataEx,
                        "Failed awarding points / updating UserData for SessionId {SessionId} / UserId {UserId}",
                        sessionId,
                        sessionLog.PlayerId
                    );
                }
            }
            // Ensure the socket is closed if it's still open
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.InternalServerError,
                        "Server closing connection",
                        CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error while closing WebSocket for SessionId: {SessionId}",
                        sessionId
                    );
                }
            }
            await _connectionManager.RemoveSocketAsync(sessionId);
        }
    }
}
