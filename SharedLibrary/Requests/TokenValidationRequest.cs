namespace SharedLibrary.Requests;

public class TokenValidationRequest
{
    public required string UserId { get; set; }
    public required string DeviceId { get; set; }
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
}
