namespace SharedLibrary.Responses;

public class LoginResult
{
    public required string UserId { get; set; }
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}
