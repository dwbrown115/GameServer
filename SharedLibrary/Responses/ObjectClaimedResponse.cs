using Newtonsoft.Json;

namespace SharedLibrary.Responses
{
    public class ObjectClaimedResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "object_claimed_response";

        [JsonProperty("session_id")]
        public required string SessionId { get; set; }

        [JsonProperty("status")]
        public required string Status { get; set; }
    }
}
