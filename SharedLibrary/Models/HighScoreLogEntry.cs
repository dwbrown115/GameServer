using System;
using Newtonsoft.Json;

namespace SharedLibrary.Models
{
    // Represents a single entry inside Leaderboard.HighScoreLog JSON array
    public class HighScoreLogEntry
    {
        public int HighScoreAtTime { get; set; }

        // Original request used key "HighScoreAtTImestamp" (capital I). Map that JSON key to a properly cased property name.
        [JsonProperty("HighScoreAtTImestamp")] // Keeps external JSON schema exactly as specified
        public DateTime HighScoreAtTimestamp { get; set; }
    }
}
