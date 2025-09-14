using GameServer.GameModules.Abstractions;

namespace GameServer.GameModules.DefaultGame;

public class DefaultMessageRegistry : IWebSocketMessageRegistry
{
    private readonly Dictionary<string, IGameMessageHandler> _handlers;

    public DefaultMessageRegistry(IEnumerable<IGameMessageHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.RequestType, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetHandler(string? requestType, out IGameMessageHandler handler)
    {
        if (requestType == null)
        {
            handler = null!;
            return false;
        }
        return _handlers.TryGetValue(requestType, out handler!);
    }
}
