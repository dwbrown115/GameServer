using System.Net.WebSockets;
using GameServer.GameModules.Abstractions;
using GameServer.Models;
using GameServer.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLibrary.Common;
using SharedLibrary.Modules.AgarSurvivor.Responses;

namespace GameServer.GameModules.AgarSurvivor.Handlers;

public class SpawnItemHandler : IGameMessageHandler
{
    public string RequestType => "spawn_item_request";

    private readonly Settings _settings;

    public SpawnItemHandler(Settings settings)
    {
        _settings = settings;
    }

    public async Task HandleAsync(
        JObject message,
        WebSocket socket,
        GameDbContext dbContext,
        SharedLibrary.Models.PlayerSessionLog sessionLog,
        CancellationToken ct = default
    )
    {
        var sessionId = message["session_id"]?.Value<string>() ?? sessionLog.SessionId;
        var playerPos = message["player_position"]?.ToObject<SharedLibrary.Common.Position>();
        var spawnRadius = message["spawn_radius"]?.Value<float?>() ?? 0f;
        var attemptTs = message["spawn_attempt_timestamp"]?.ToObject<DateTime?>();

        sessionLog.SpawnRequests = (sessionLog.SpawnRequests ?? 0) + 1;

        var spawnResponse = new SpawnRequestResponse { SessionId = sessionId };

        if (playerPos == null)
        {
            spawnResponse.Granted = false;
            spawnResponse.UniqueId = null;
            spawnResponse.SpawnPosition = null;
        }
        else
        {
            bool granted;
            if (sessionLog.LastSpawnAttempt == null)
            {
                granted = true;
            }
            else
            {
                var elapsed = DateTime.UtcNow - sessionLog.LastSpawnAttempt.Value;
                var minElapsed = TimeSpan.FromSeconds(_settings.SpawnCooldownSeconds ?? 5.0);
                granted = elapsed >= minElapsed;
            }

            spawnResponse.Granted = granted;
            if (granted)
            {
                sessionLog.ValidatedSpawns = (sessionLog.ValidatedSpawns ?? 0) + 1;
                var uniqueId = Utilities.NumberGeneratorUtility.GenerateValidNumber(10);
                var spawnPosition = Utilities.SpawnPositionUtility.GenerateRandomPositionInCircle(
                    playerPos!,
                    spawnRadius,
                    _settings.NoSpawnRadius ?? 1.0f
                );
                spawnResponse.UniqueId = uniqueId;
                spawnResponse.SpawnPosition = spawnPosition;

                var lifecycleLogs = !string.IsNullOrEmpty(sessionLog.ObjectLifecycleLog)
                    ? JsonConvert.DeserializeObject<
                        List<SharedLibrary.Modules.AgarSurvivor.Models.ObjectLifecycleLog>
                    >(sessionLog.ObjectLifecycleLog) ?? new()
                    : new List<SharedLibrary.Modules.AgarSurvivor.Models.ObjectLifecycleLog>();

                lifecycleLogs.Add(
                    new SharedLibrary.Modules.AgarSurvivor.Models.ObjectLifecycleLog
                    {
                        Id = uniqueId,
                        ClientSpawnedTime = attemptTs,
                        ServerSpawnedTime = DateTime.UtcNow,
                        ClaimedTime = null,
                        Coordinates = spawnPosition,
                    }
                );

                sessionLog.ObjectLifecycleLog = JsonConvert.SerializeObject(lifecycleLogs);

                sessionLog.LastKnownPosition = playerPos;
                sessionLog.LastSpawnAttempt = DateTime.UtcNow;
                sessionLog.CurrentSpawnRadius = spawnRadius;

                var positionLogList = !string.IsNullOrEmpty(sessionLog.PlayerPositionLog)
                    ? JsonConvert.DeserializeObject<
                        List<SharedLibrary.Modules.AgarSurvivor.Models.PlayerPositionLogEntry>
                    >(sessionLog.PlayerPositionLog) ?? new()
                    : new List<SharedLibrary.Modules.AgarSurvivor.Models.PlayerPositionLogEntry>();

                positionLogList.Add(
                    new SharedLibrary.Modules.AgarSurvivor.Models.PlayerPositionLogEntry
                    {
                        X = playerPos.X,
                        Y = playerPos.Y,
                        PlayerId = sessionLog.PlayerId,
                        Timestamp = DateTime.UtcNow,
                    }
                );

                sessionLog.PlayerPositionLog = JsonConvert.SerializeObject(positionLogList);
            }
        }

        var resp = JsonConvert.SerializeObject(spawnResponse);
        var bytes = System.Text.Encoding.UTF8.GetBytes(resp);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
