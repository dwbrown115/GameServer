namespace SharedLibrary.Requests;

public class WebSocketAuthRequest
{
    public required string DeviceId { get; set; }
    public required string UserId { get; set; }
    public required string JwtToken { get; set; }
    public required string RefreshToken { get; set; }
}
