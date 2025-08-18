using System.Text.Json.Serialization;
using SharedLibrary.Common;

namespace SharedLibrary.Pings
{
    public class PlayerPing
    {
        [JsonPropertyName("session_id")]
        public required string SessionId { get; set; }

        [JsonPropertyName("player_id")]
        public required string PlayerId { get; set; }

        [JsonPropertyName("current_position")]
        public required Position CurrentPosition { get; set; }

        [JsonPropertyName("radius")]
        public float Radius { get; set; }

        [JsonPropertyName("last_spawn_attempt")]
        public DateTime LastSpawnAttempt { get; set; }
    }
}
