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

        [JsonProperty("points_after_purchase", NullValueHandling = NullValueHandling.Ignore)]
        public int? PointsAfterPurchase { get; set; }

        [JsonProperty("owned_skin_ids", NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? OwnedSkinIds { get; set; }
    }
}
