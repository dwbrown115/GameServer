using Newtonsoft.Json;

namespace SharedLibrary.Modules.AgarSurvivor.Requests
{
    public class BuySkinRequest
    {
        [JsonProperty("request_type")]
        public string RequestType { get; set; } = "buy_skin_request";

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("skinId")]
        public string SkinId { get; set; } = string.Empty;
    }
}
