using GameServer.GameModules.Abstractions;

namespace GameServer.GameModules.AgarSurvivor;

public class MessageRegistry : IWebSocketMessageRegistry
{
    private readonly Dictionary<string, IGameMessageHandler> _handlers;

    public MessageRegistry(IEnumerable<IGameMessageHandler> handlers)
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
