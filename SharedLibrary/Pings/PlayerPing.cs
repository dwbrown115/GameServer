using System;
using Newtonsoft.Json;
using SharedLibrary.Common;

namespace SharedLibrary.Pings
{
    public class PlayerPing
    {
        [JsonProperty("request_type")]
        public string RequestType { get; set; } = "player_ping";

        [JsonProperty("session_id")]
        public required string SessionId { get; set; }

        [JsonProperty("PlayerId")]
        public string? PlayerId { get; set; }

        [JsonProperty("attempted_client_score")]
        public int AttemptedClientScore { get; set; }

        [JsonProperty("CurrentPosition")]
        public Position? CurrentPosition { get; set; }

        [JsonProperty("radius")]
        public float Radius { get; set; }

        [JsonProperty("last_spawn_attempt")]
        public DateTime LastSpawnAttempt { get; set; }
    }
}