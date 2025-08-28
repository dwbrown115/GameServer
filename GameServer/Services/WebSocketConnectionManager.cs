using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Services;

public class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly ILogger<WebSocketConnectionManager> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public WebSocketConnectionManager(
        ILogger<WebSocketConnectionManager> logger,
        IServiceScopeFactory scopeFactory
    )
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public void AddSocket(string sessionId, WebSocket socket)
    {
        _sockets.TryAdd(sessionId, socket);
        _logger.LogInformation("WebSocket connected for SessionId: {SessionId}", sessionId);
    }

    public async Task RemoveSocketAsync(string sessionId)
    {
        if (_sockets.TryRemove(sessionId, out var socket))
        {
            // Stamp session end in DB (covers forced server-side disconnects where handler 'finally' might not run)
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                var sessionLog = await db.PlayerSessionLogs.FirstOrDefaultAsync(s =>
                    s.SessionId == sessionId && s.SessionEnd == null
                );
                if (sessionLog != null)
                {
                    sessionLog.SessionEnd = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    _logger.LogInformation(
                        "Session {SessionId} end timestamp persisted via manager removal.",
                        sessionId
                    );
                }
            }
            catch (Exception endEx)
            {
                _logger.LogError(
                    endEx,
                    "Failed to persist session end for SessionId {SessionId} during removal.",
                    sessionId
                );
            }

            try
            {
                if (
                    socket.State == WebSocketState.Open
                    || socket.State == WebSocketState.CloseReceived
                )
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed by server.",
                        CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error closing WebSocket for SessionId: {SessionId}",
                    sessionId
                );
            }
            finally
            {
                socket.Dispose();
                _logger.LogInformation(
                    "WebSocket disconnected for SessionId: {SessionId}",
                    sessionId
                );
            }
        }
    }
}
