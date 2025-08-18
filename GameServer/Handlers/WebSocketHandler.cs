using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using GameServer.Models;
using GameServer.Services;
using GameServer.Utilities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLibrary.Models;
using SharedLibrary.Pings;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Handlers;

public class WebSocketHandler : IWebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IWebSocketConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Settings _settings;

    public WebSocketHandler(
        ILogger<WebSocketHandler> logger,
        IWebSocketConnectionManager connectionManager,
        IServiceScopeFactory scopeFactory,
        Settings settings
    )
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _scopeFactory = scopeFactory;
        _settings = settings;
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
                        // Attempt to deserialize as PlayerPing
                        var playerPing = JsonConvert.DeserializeObject<PlayerPing>(messageString);
                        if (
                            playerPing != null
                            && !string.IsNullOrEmpty(playerPing.SessionId)
                            && !string.IsNullOrEmpty(playerPing.PlayerId)
                        )
                        {
                            _logger.LogInformation(
                                "Player {PlayerId} ping: SessionId={SessionId}, X={X}, Y={Y}, Radius={Radius}, LastSpawnAttempt={LastSpawnAttempt}",
                                playerPing.PlayerId,
                                playerPing.SessionId,
                                playerPing.CurrentPosition?.X ?? 0.0f,
                                playerPing.CurrentPosition?.Y ?? 0.0f,
                                playerPing.Radius,
                                playerPing.LastSpawnAttempt
                            );

                            // Update LastKnownPosition in PlayerSessionLog
                            if (sessionLog != null && playerPing.CurrentPosition != null)
                            {
                                sessionLog.LastKnownPosition = playerPing.CurrentPosition;
                                await dbContext.SaveChangesAsync();
                            }

                            // Send response
                            var response = new PlayerPingResponse
                            {
                                SessionId = sessionId,
                                Status = "Received by server at " + DateTime.UtcNow.ToString("o"),
                            };
                            var responseString = JsonConvert.SerializeObject(response);
                            var responseBytes = Encoding.UTF8.GetBytes(responseString);
                            await socket.SendAsync(
                                new ArraySegment<byte>(responseBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            );
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Attempting to deserialize as SpawnItemRequest. Raw message: {Message}",
                                messageString
                            );
                            // Attempt to deserialize as SpawnItemRequest
                            var spawnRequest = JsonConvert.DeserializeObject<SpawnItemRequest>(
                                messageString
                            );
                            _logger.LogInformation(
                                "SpawnItemRequest deserialization result: {IsNull}",
                                spawnRequest == null ? "null" : "not null"
                            );

                            if (spawnRequest != null)
                            {
                                sessionLog.SpawnRequests = (sessionLog.SpawnRequests ?? 0) + 1;

                                // Manually deserialize PlayerPosition
                                JObject json = JObject.Parse(messageString);
                                JToken playerPositionToken = json["player_position"];
                                if (playerPositionToken != null)
                                {
                                    spawnRequest.PlayerPosition =
                                        playerPositionToken.ToObject<SharedLibrary.Common.Position>();
                                    _logger.LogInformation(
                                        "Manually deserialized PlayerPosition: X={X}, Y={Y}",
                                        spawnRequest.PlayerPosition?.X ?? 0.0f,
                                        spawnRequest.PlayerPosition?.Y ?? 0.0f
                                    );
                                }

                                // Deny if PlayerPosition is null
                                if (spawnRequest.PlayerPosition == null)
                                {
                                    _logger.LogWarning(
                                        "Spawn attempt denied: PlayerPosition is null."
                                    );
                                    var deniedResponse = new SpawnRequestResponse
                                    {
                                        SessionId = spawnRequest.SessionId,
                                        Granted = false,
                                        UniqueId = null,
                                        SpawnPosition = null,
                                    };
                                    var deniedResponseString = JsonConvert.SerializeObject(
                                        deniedResponse
                                    );
                                    var deniedResponseBytes = Encoding.UTF8.GetBytes(
                                        deniedResponseString
                                    );
                                    await socket.SendAsync(
                                        new ArraySegment<byte>(deniedResponseBytes),
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None
                                    );
                                    _logger.LogInformation(
                                        "Spawn response sent (denied due to null PlayerPosition)."
                                    );
                                    await dbContext.SaveChangesAsync(); // Save changes before returning
                                    return; // Exit early
                                }

                                _logger.LogInformation(
                                    "Spawn Item Request details: PlayerX={PlayerX}, PlayerY={PlayerY}, Timestamp={Timestamp}, Radius={Radius}",
                                    spawnRequest.PlayerPosition?.X ?? 0.0f,
                                    spawnRequest.PlayerPosition?.Y ?? 0.0f,
                                    spawnRequest.SpawnAttemptTimestamp,
                                    spawnRequest.SpawnRadius
                                );

                                bool granted = false;
                                var spawnResponse = new SpawnRequestResponse
                                {
                                    SessionId = spawnRequest.SessionId,
                                };

                                // Check spawn attempt time using server's current time
                                if (sessionLog.LastSpawnAttempt == null)
                                {
                                    // First spawn attempt, grant it
                                    granted = true;
                                    _logger.LogInformation("First spawn attempt granted.");
                                }
                                else
                                {
                                    TimeSpan timeSinceLastSpawn =
                                        DateTime.UtcNow - sessionLog.LastSpawnAttempt.Value;
                                    TimeSpan minimumElapsedTime = TimeSpan.FromSeconds(
                                        _settings.SpawnCooldownSeconds ?? 5.0
                                    ); // Use configurable cooldown

                                    if (timeSinceLastSpawn >= minimumElapsedTime)
                                    {
                                        granted = true;
                                        _logger.LogInformation(
                                            "Spawn attempt granted after cooldown."
                                        );
                                    }
                                    else
                                    {
                                        granted = false;
                                        _logger.LogWarning(
                                            "Spawn attempt denied: Not enough time elapsed since last spawn."
                                        );
                                    }
                                }

                                spawnResponse.Granted = granted;

                                if (granted)
                                {
                                    sessionLog.ValidatedSpawns =
                                        (sessionLog.ValidatedSpawns ?? 0) + 1;
                                    _logger.LogInformation(
                                        "Generating unique ID and spawn position."
                                    );
                                    // Generate unique ID and spawn position
                                    var uniqueId = NumberGeneratorUtility.GenerateValidNumber(10); // Example length
                                    var spawnPosition =
                                        SpawnPositionUtility.GenerateRandomPositionInCircle(
                                            spawnRequest.PlayerPosition!,
                                            spawnRequest.SpawnRadius,
                                            _settings.NoSpawnRadius ?? 1.0f
                                        );
                                    spawnResponse.UniqueId = uniqueId;
                                    spawnResponse.SpawnPosition = spawnPosition; // Corrected line

                                    // Log the object lifecycle event
                                    var objectLifecycleLog = new ObjectLifecycleLog
                                    {
                                        Id = uniqueId,
                                        ClientSpawnedTime = spawnRequest.SpawnAttemptTimestamp,
                                        ServerSpawnedTime = DateTime.UtcNow,
                                        ClaimedTime = null,
                                        Coordinates = spawnPosition,
                                    };

                                    var lifecycleLogs = !string.IsNullOrEmpty(
                                        sessionLog.ObjectLifecycleLog
                                    )
                                        ? JsonConvert.DeserializeObject<List<ObjectLifecycleLog>>(
                                            sessionLog.ObjectLifecycleLog
                                        )
                                        : new List<ObjectLifecycleLog>();

                                    if (lifecycleLogs != null)
                                    {
                                        lifecycleLogs.Add(objectLifecycleLog);
                                        sessionLog.ObjectLifecycleLog = JsonConvert.SerializeObject(
                                            lifecycleLogs
                                        );
                                    }

                                    _logger.LogInformation(
                                        "Updating session log with spawn details."
                                    );
                                    // Update session log with current spawn attempt details using server's current time
                                    sessionLog.LastSpawnAttempt = DateTime.UtcNow;
                                    sessionLog.CurrentSpawnRadius = spawnRequest.SpawnRadius;
                                }
                                else
                                {
                                    // Set response values to null if denied
                                    spawnResponse.UniqueId = null;
                                    spawnResponse.SpawnPosition = null;
                                    _logger.LogInformation(
                                        "Spawn response values set to null (denied)."
                                    );
                                }

                                _logger.LogInformation("Sending spawn response to client.");
                                var spawnResponseString = JsonConvert.SerializeObject(
                                    spawnResponse
                                );
                                var spawnResponseBytes = Encoding.UTF8.GetBytes(
                                    spawnResponseString
                                );
                                await socket.SendAsync(
                                    new ArraySegment<byte>(spawnResponseBytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None
                                );
                                _logger.LogInformation("Spawn response sent.");
                                await dbContext.SaveChangesAsync();
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Received unhandled message: {Message}",
                                    messageString
                                );
                            }
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
