namespace SharedLibrary.Requests;

public class LogoutRequest
{
    public required string UserId { get; set; }
    public required string DeviceId { get; set; }
    public required string RefreshToken { get; set; }
}
