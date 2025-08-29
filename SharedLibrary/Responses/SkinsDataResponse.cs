using Newtonsoft.Json;

namespace SharedLibrary.Responses
{
    public class SkinsDataResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; } = "skins_data_response";

        [JsonProperty("payload")]
        public List<SkinDataItem> Payload { get; set; } = new();
    }

    public class SkinDataItem
    {
        [JsonProperty("SkinId")]
        public string SkinId { get; set; } = string.Empty; // maps to Skins.UUID

        [JsonProperty("HexValue")]
        public string HexValue { get; set; } = string.Empty;

        [JsonProperty("Price")]
        public int Price { get; set; }
    }
}
