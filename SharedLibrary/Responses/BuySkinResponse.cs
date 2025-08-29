using Newtonsoft.Json;

namespace SharedLibrary.Responses
{
    public class BuySkinResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "buy_skin_request";

        [JsonProperty("approved")]
        public bool Approved { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }
}
