using System;
using Newtonsoft.Json;
using SharedLibrary.Common;

namespace SharedLibrary.Modules.AgarSurvivor.Requests
{
    public class SpawnItemRequest
    {
        [JsonProperty("request_type")]
        public string RequestType { get; set; } = "spawn_item_request";

        [JsonProperty("session_id")]
        public required string SessionId { get; set; }

        [JsonProperty("player_position")]
        public required Position PlayerPosition { get; set; }

        [JsonProperty("spawn_attempt_timestamp")]
        public DateTime SpawnAttemptTimestamp { get; set; }

        [JsonProperty("spawn_radius")]
        public float SpawnRadius { get; set; }
    }
}
