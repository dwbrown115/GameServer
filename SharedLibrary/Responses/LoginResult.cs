namespace SharedLibrary.Responses;

public class LoginResult
{
    public string UserId { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}