using System.Text.Json.Serialization;
using SharedLibrary.Common;

namespace SharedLibrary.Modules.AgarSurvivor.Models
{
    public class ObjectLifecycleLog
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("clientSpawnedTime")]
        public DateTime? ClientSpawnedTime { get; set; }

        [JsonPropertyName("serverSpawnedTime")]
        public DateTime? ServerSpawnedTime { get; set; }

        [JsonPropertyName("claimedTime")]
        public DateTime? ClaimedTime { get; set; }

        [JsonPropertyName("coordinates")]
        public required Position Coordinates { get; set; }
    }
}
