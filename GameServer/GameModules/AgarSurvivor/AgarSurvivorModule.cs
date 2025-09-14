using GameServer.GameModules.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.GameModules.AgarSurvivor;

public class AgarSurvivorModule : IGameModule
{
    public string Name => "AgarSurvivor";

    public void AddServices(IServiceCollection services)
    {
        services.AddSingleton<IWebSocketMessageRegistry, MessageRegistry>();
        services.AddSingleton<IGameMessageHandler, Handlers.PlayerPingHandler>();
        services.AddSingleton<IGameMessageHandler, Handlers.SpawnItemHandler>();
        services.AddSingleton<IGameMessageHandler, Handlers.ObjectClaimedHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        AgarSurvivorEndpoints.Map(endpoints);
    }
}
