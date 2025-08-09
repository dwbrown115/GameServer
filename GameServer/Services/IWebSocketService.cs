using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Services;

public interface IWebSocketService
{
    Task<WebSocketAuthResponse> AuthenticateAsync(WebSocketAuthRequest request);
}
