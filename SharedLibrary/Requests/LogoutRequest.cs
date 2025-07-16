namespace SharedLibrary.Requests;

public class LogoutRequest
{
    public string UserId { get; set; }
    public string DeviceId { get; set; }
    public string RefreshToken { get; set; }
}
