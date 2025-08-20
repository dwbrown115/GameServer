using Newtonsoft.Json;

namespace SharedLibrary.Common
{
    public class Position
    {
        [JsonProperty("X")]
        public float X { get; set; }

        [JsonProperty("Y")]
        public float Y { get; set; }
    }
}