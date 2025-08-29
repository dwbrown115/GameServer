using Newtonsoft.Json;

namespace SharedLibrary.Models
{
    public class PointsLogEntry
    {
        [JsonProperty("PointsAtTime")]
        public int PointsAtTime { get; set; }

        [JsonProperty("PointsAtTimestamp")]
        public DateTime PointsAtTimestamp { get; set; }
    }
}
