namespace GameServer.GameModules.Abstractions;

public interface IWebSocketMessageRegistry
{
    bool TryGetHandler(string? requestType, out IGameMessageHandler handler);
}
