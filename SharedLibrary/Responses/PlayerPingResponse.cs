using System.Text.Json.Serialization;

namespace SharedLibrary.Responses
{
    public class PlayerPingResponse
    {
        // [JsonPropertyName("session_id")]
        public required string SessionId { get; set; }

        // [JsonPropertyName("status")]
        public required string Status { get; set; }
    }
}
