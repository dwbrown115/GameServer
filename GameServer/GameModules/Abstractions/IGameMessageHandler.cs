using System.Net.WebSockets;
using Newtonsoft.Json.Linq;

namespace GameServer.GameModules.Abstractions;

public interface IGameMessageHandler
{
    string RequestType { get; }

    Task HandleAsync(
        JObject message,
        WebSocket socket,
        GameDbContext dbContext,
        SharedLibrary.Models.PlayerSessionLog sessionLog,
        CancellationToken ct = default
    );
}
