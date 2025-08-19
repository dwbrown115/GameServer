using Newtonsoft.Json;

namespace SharedLibrary.Responses
{
    public class PlayerPingResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "player_ping_response";

        [JsonProperty("session_id")]
        public required string SessionId { get; set; }

        [JsonProperty("status")]
        public required string Status { get; set; }
    }
}
