using Newtonsoft.Json;

namespace SharedLibrary.Modules.AgarSurvivor.Responses
{
    public class PlayerPingResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "player_ping_response";

        [JsonProperty("session_id")]
        public required string SessionId { get; set; }

        [JsonProperty("status")]
        public required string Status { get; set; }

        [JsonProperty("server_score")]
        public int ServerScore { get; set; }
    }
}
