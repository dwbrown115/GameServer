using System.Net.WebSockets;
using GameServer.GameModules.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLibrary.Modules.AgarSurvivor.Pings;
using SharedLibrary.Modules.AgarSurvivor.Responses;

namespace GameServer.GameModules.AgarSurvivor.Handlers;

public class PlayerPingHandler : IGameMessageHandler
{
    public string RequestType => "player_ping";

    public async Task HandleAsync(
        JObject message,
        WebSocket socket,
        GameDbContext dbContext,
        SharedLibrary.Models.PlayerSessionLog sessionLog,
        CancellationToken ct = default
    )
    {
        var playerPing = message.ToObject<PlayerPing>();
        if (playerPing == null)
            return;

        sessionLog.LastKnownPosition = playerPing.CurrentPosition;
        sessionLog.AttemptedClientScore = playerPing.AttemptedClientScore;

        var positionLogList = !string.IsNullOrEmpty(sessionLog.PlayerPositionLog)
            ? JsonConvert.DeserializeObject<
                List<SharedLibrary.Modules.AgarSurvivor.Models.PlayerPositionLogEntry>
            >(sessionLog.PlayerPositionLog) ?? new()
            : new List<SharedLibrary.Modules.AgarSurvivor.Models.PlayerPositionLogEntry>();

        positionLogList.Add(
            new SharedLibrary.Modules.AgarSurvivor.Models.PlayerPositionLogEntry
            {
                X = playerPing.CurrentPosition?.X ?? 0.0f,
                Y = playerPing.CurrentPosition?.Y ?? 0.0f,
                PlayerId = sessionLog.PlayerId,
                Timestamp = DateTime.UtcNow,
            }
        );

        sessionLog.PlayerPositionLog = JsonConvert.SerializeObject(positionLogList);
        await dbContext.SaveChangesAsync(ct);

        var status = sessionLog.ScoreServer == sessionLog.AttemptedClientScore ? "Ok" : "Bad";

        var response = new PlayerPingResponse
        {
            SessionId = sessionLog.SessionId,
            Status = status,
            ServerScore = sessionLog.ScoreServer,
        };
        var responseString = JsonConvert.SerializeObject(response);
        var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseString);
        await socket.SendAsync(
            new ArraySegment<byte>(responseBytes),
            WebSocketMessageType.Text,
            true,
            ct
        );
    }
}
