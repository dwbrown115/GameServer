namespace SharedLibrary.Responses
{
    [Obsolete("PlayerPositionResponse is deprecated, use a new response type for PlayerPing instead.")]
    public class PlayerPositionResponse
    {
        public float X { get; set; }
        public float Y { get; set; }
        public string Status { get; set; }
    }
}
