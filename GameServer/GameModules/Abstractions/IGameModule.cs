using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.GameModules.Abstractions;

public interface IGameModule
{
    string Name { get; }
    void AddServices(IServiceCollection services);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
