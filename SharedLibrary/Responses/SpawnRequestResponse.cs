using System.Text.Json.Serialization;
using SharedLibrary.Common;

namespace SharedLibrary.Responses
{
    public class SpawnRequestResponse
    {
        [JsonPropertyName("spawn_position")]
        public SharedLibrary.Common.Position? SpawnPosition { get; set; }

        [JsonPropertyName("unique_id")]
        public string? UniqueId { get; set; }

        [JsonPropertyName("session_id")]
        public required string SessionId { get; set; }

        [JsonPropertyName("granted")]
        public bool Granted { get; set; }
    }
}
