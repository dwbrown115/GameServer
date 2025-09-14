using Newtonsoft.Json;

namespace SharedLibrary.Modules.AgarSurvivor.Responses
{
    public class SpawnRequestResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "spawn_request_response";

        [JsonProperty("spawn_position")]
        public SharedLibrary.Common.Position? SpawnPosition { get; set; }

        [JsonProperty("unique_id")]
        public string? UniqueId { get; set; }

        [JsonProperty("session_id")]
        public required string SessionId { get; set; }

        [JsonProperty("granted")]
        public bool Granted { get; set; }
    }
}
