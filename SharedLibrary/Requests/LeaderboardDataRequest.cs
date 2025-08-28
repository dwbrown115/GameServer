using Newtonsoft.Json;

namespace SharedLibrary.Requests
{
    public class LeaderboardDataRequest
    {
        [JsonProperty("request_type")]
        public string RequestType { get; set; } = "leaderboard_data_request";
    }
}
