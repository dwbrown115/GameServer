namespace SharedLibrary.Responses;

public class AuthenticationResponse
{
    public string UserId { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}
