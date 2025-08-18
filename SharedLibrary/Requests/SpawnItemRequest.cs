using System;
using System.Text.Json.Serialization;
using SharedLibrary.Common;

namespace SharedLibrary.Requests
{
    public class SpawnItemRequest
    {
        [JsonPropertyName("session_id")]
        public required string SessionId { get; set; }

        [JsonPropertyName("player_position")]
        public required Position PlayerPosition { get; set; }

        [JsonPropertyName("spawn_attempt_timestamp")]
        public DateTime SpawnAttemptTimestamp { get; set; }

        [JsonPropertyName("spawn_radius")]
        public float SpawnRadius { get; set; }
    }
}
