using System;
using Newtonsoft.Json;
using SharedLibrary.Common;

namespace SharedLibrary.Requests
{
    public class ObjectClaimedRequest
    {
        [JsonProperty("id")]
        public required string Id { get; set; }

        [JsonProperty("clientSpawnedTime")]
        public DateTime? ClientSpawnedTime { get; set; }

        [JsonProperty("claimedTime")]
        public DateTime? ClaimedTime { get; set; }

        [JsonProperty("coordinates")]
        public required Position Coordinates { get; set; }
    }
}
