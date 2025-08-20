using System;
using Newtonsoft.Json;

namespace SharedLibrary.Models
{
    public class ScoreLogEntry
    {
        [JsonProperty("server_score")]
        public int ServerScore { get; set; }

        [JsonProperty("object_id")]
        public required string ObjectId { get; set; }

        [JsonProperty("player_id")]
        public required string PlayerId { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
