using System.Net.WebSockets;

namespace GameServer.Services;

public interface IWebSocketConnectionManager
{
    void AddSocket(string sessionId, WebSocket socket);
    Task RemoveSocketAsync(string sessionId);
}
