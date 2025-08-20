using System;
using Newtonsoft.Json;

namespace SharedLibrary.Models
{
    public class PlayerPositionLogEntry
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("player_id")]
        public required string PlayerId { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
