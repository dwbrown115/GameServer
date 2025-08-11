using System.Net.WebSockets;
using System.Text;
using GameServer.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SharedLibrary.Requests;

namespace GameServer.Handlers;

public class WebSocketHandler : IWebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IWebSocketConnectionManager _connectionManager;
    private readonly IServiceScopeFactory _scopeFactory;

    public WebSocketHandler(ILogger<WebSocketHandler> logger, IWebSocketConnectionManager connectionManager, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _scopeFactory = scopeFactory;
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

        var sessionLog = await dbContext.PlayerSessionLogs
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.SessionEnd == null);

        if (sessionLog == null)
        {
            _logger.LogWarning("WebSocket connection rejected for invalid or expired SessionId: {SessionId}", sessionId);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid or expired session.");
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        _connectionManager.AddSocket(sessionId, socket);
        _logger.LogInformation("WebSocket connection established for PlayerId: {PlayerId} with SessionId: {SessionId}", sessionLog.PlayerId, sessionId);

        try
        {
            var buffer = new byte[1024 * 4];
            var receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!receiveResult.CloseStatus.HasValue)
            {
                var messageString = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);

                try
                {
                    var positionMessage = JsonConvert.DeserializeObject<PlayerPositionMessage>(messageString);
                    if (positionMessage != null)
                    {
                        _logger.LogInformation("Player {PlayerId} position update: X={X}, Y={Y}", sessionLog.PlayerId, positionMessage.X, positionMessage.Y);

                        // Create and send the response back to the client
                        var response = new SharedLibrary.Responses.PlayerPositionResponse
                        {
                            X = positionMessage.X,
                            Y = positionMessage.Y,
                            Status = "Received by server at " + DateTime.UtcNow.ToString("o")
                        };
                        var responseString = JsonConvert.SerializeObject(response);
                        var responseBytes = Encoding.UTF8.GetBytes(responseString);
                        await socket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning("Could not deserialize message from SessionId {SessionId}. Error: {Error}. Message: {Message}", sessionId, jsonEx.Message, messageString);
                }

                receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogInformation("WebSocket connection closed for SessionId {SessionId}. Reason: {Message}", sessionId, ex.Message);
        }
        finally
        {
            await _connectionManager.RemoveSocketAsync(sessionId);
        }
    }
}