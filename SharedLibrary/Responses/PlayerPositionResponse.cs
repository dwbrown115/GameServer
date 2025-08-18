namespace SharedLibrary.Responses
{
    [Obsolete("PlayerPositionResponse is deprecated, use a new response type for PlayerPing instead.")]
    public class PlayerPositionResponse
    {
        public float X { get; set; }
        public float Y { get; set; }
        public required string Status { get; set; }
    }
}
