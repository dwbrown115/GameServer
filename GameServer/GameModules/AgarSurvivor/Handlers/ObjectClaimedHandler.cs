using System.Net.WebSockets;
using GameServer.GameModules.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLibrary.Modules.AgarSurvivor.Requests;
using SharedLibrary.Modules.AgarSurvivor.Responses;

namespace GameServer.GameModules.AgarSurvivor.Handlers;

public class ObjectClaimedHandler : IGameMessageHandler
{
    public string RequestType => "object_claimed_request";

    public async Task HandleAsync(
        JObject message,
        WebSocket socket,
        GameDbContext dbContext,
        SharedLibrary.Models.PlayerSessionLog sessionLog,
        CancellationToken ct = default
    )
    {
        var envelope =
            message.ToObject<ClaimObjectWebSocketEnvelope>() ?? new ClaimObjectWebSocketEnvelope();
        var obj = envelope.ClaimedObject;
        if (obj == null)
            return;

        var lifecycleLogs = !string.IsNullOrEmpty(sessionLog.ObjectLifecycleLog)
            ? JsonConvert.DeserializeObject<
                List<SharedLibrary.Modules.AgarSurvivor.Models.ObjectLifecycleLog>
            >(sessionLog.ObjectLifecycleLog) ?? new()
            : new List<SharedLibrary.Modules.AgarSurvivor.Models.ObjectLifecycleLog>();

        var objectLog = lifecycleLogs.FirstOrDefault(o => o.Id == obj.Id);
        if (objectLog != null)
        {
            if (objectLog.ClaimedTime == null)
            {
                objectLog.ClaimedTime = obj.ClaimedTime;
                objectLog.ClientSpawnedTime = obj.ClientSpawnedTime;

                sessionLog.ScoreServer++;

                var scoreLogs = !string.IsNullOrEmpty(sessionLog.ScoreLog)
                    ? JsonConvert.DeserializeObject<
                        List<SharedLibrary.Modules.AgarSurvivor.Models.ScoreLogEntry>
                    >(sessionLog.ScoreLog) ?? new()
                    : new List<SharedLibrary.Modules.AgarSurvivor.Models.ScoreLogEntry>();

                scoreLogs.Add(
                    new SharedLibrary.Modules.AgarSurvivor.Models.ScoreLogEntry
                    {
                        ServerScore = sessionLog.ScoreServer,
                        ObjectId = obj.Id,
                        PlayerId = sessionLog.PlayerId,
                        Timestamp = DateTime.UtcNow,
                    }
                );

                sessionLog.ScoreLog = JsonConvert.SerializeObject(scoreLogs);
                sessionLog.ObjectLifecycleLog = JsonConvert.SerializeObject(lifecycleLogs);
                await dbContext.SaveChangesAsync(ct);

                var ok = new ObjectClaimedResponse
                {
                    SessionId = sessionLog.SessionId,
                    Status = "Ok",
                };
                var okStr = JsonConvert.SerializeObject(ok);
                var okBytes = System.Text.Encoding.UTF8.GetBytes(okStr);
                await socket.SendAsync(
                    new ArraySegment<byte>(okBytes),
                    WebSocketMessageType.Text,
                    true,
                    ct
                );
            }
            else
            {
                var bad = new ObjectClaimedResponse
                {
                    SessionId = sessionLog.SessionId,
                    Status = "Bad",
                };
                var badStr = JsonConvert.SerializeObject(bad);
                var badBytes = System.Text.Encoding.UTF8.GetBytes(badStr);
                await socket.SendAsync(
                    new ArraySegment<byte>(badBytes),
                    WebSocketMessageType.Text,
                    true,
                    ct
                );
            }
        }
    }

    private class ClaimObjectWebSocketEnvelope
    {
        [Newtonsoft.Json.JsonProperty("session_id")]
        public string SessionId { get; set; } = string.Empty;

        [Newtonsoft.Json.JsonProperty("claimedObject")]
        public ObjectClaimedRequest? ClaimedObject { get; set; }
    }
}
