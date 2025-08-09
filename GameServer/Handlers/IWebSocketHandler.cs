namespace GameServer.Handlers;

public interface IWebSocketHandler
{
    Task HandleAsync(HttpContext context);
}
