using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace GameServer.Services;

public class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly ILogger<WebSocketConnectionManager> _logger;

    public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
    {
        _logger = logger;
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
