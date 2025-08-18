using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SharedLibrary.Common;

namespace SharedLibrary.Models
{
    public class ObjectLifecycleLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("clientSpawnedTime")]
        public DateTime? ClientSpawnedTime { get; set; }

        [JsonPropertyName("serverSpawnedTime")]
        public DateTime ServerSpawnedTime { get; set; }

        [JsonPropertyName("claimedTime")]
        public DateTime? ClaimedTime { get; set; }

        [JsonPropertyName("coordinates")]
        public required Position Coordinates { get; set; }
    }
}
