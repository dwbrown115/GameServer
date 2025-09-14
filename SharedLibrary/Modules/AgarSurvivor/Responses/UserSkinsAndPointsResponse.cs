using Newtonsoft.Json;

namespace SharedLibrary.Modules.AgarSurvivor.Responses
{
    public class UserSkinsAndPointsResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "user_skins_points_response";

        [JsonProperty("UserId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("Points")]
        public int Points { get; set; }

        [JsonProperty("OwnedSkinIds")]
        public List<string> OwnedSkinIds { get; set; } = new();
    }
}
