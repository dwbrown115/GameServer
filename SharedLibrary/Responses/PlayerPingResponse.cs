using System.Text.Json.Serialization;

namespace SharedLibrary.Responses
{
    public class PlayerPingResponse
    {
        // [JsonPropertyName("session_id")]
        public string SessionId { get; set; }

        // [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}
