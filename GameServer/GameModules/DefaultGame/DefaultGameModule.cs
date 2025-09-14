using GameServer.GameModules.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.GameModules.DefaultGame;

public class DefaultGameModule : IGameModule
{
    public string Name => "Default";

    public void AddServices(IServiceCollection services)
    {
        services.AddSingleton<IWebSocketMessageRegistry, DefaultMessageRegistry>();
        services.AddSingleton<IGameMessageHandler, Handlers.PlayerPingHandler>();
        services.AddSingleton<IGameMessageHandler, Handlers.SpawnItemHandler>();
        services.AddSingleton<IGameMessageHandler, Handlers.ObjectClaimedHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No extra endpoints for the default module.
    }
}
