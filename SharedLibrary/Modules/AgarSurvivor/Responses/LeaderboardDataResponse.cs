using Newtonsoft.Json;

namespace SharedLibrary.Modules.AgarSurvivor.Responses
{
    public class LeaderboardDataResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "leaderboard_data_response";

        [JsonProperty("payload")]
        public List<LeaderboardDataItem> Payload { get; set; } = new();
    }

    public class LeaderboardDataItem
    {
        [JsonProperty("Username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("PlayerHighestScore")]
        public int PlayerHighestScore { get; set; }
    }
}
