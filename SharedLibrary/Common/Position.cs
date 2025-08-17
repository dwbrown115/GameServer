using System.Text.Json.Serialization;

namespace SharedLibrary.Common
{
    public class Position
    {
        [JsonPropertyName("X")]
        public float X { get; set; }

        [JsonPropertyName("Y")]
        public float Y { get; set; }
    }
}
