namespace SharedLibrary.Requests;

public class TokenValidationRequest
{
    public string UserId { get; set; }
    public string DeviceId { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
}

