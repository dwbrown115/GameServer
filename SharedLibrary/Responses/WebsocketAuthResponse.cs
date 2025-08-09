namespace SharedLibrary.Responses;

public class WebSocketAuthResponse
{
    public required bool Authenticated { get; set; }
    public string? SessionId { get; set; } // Only if authenticated
    public string? Reason { get; set; } // Only if not authenticated
    public string? Token { get; set; } // The latest JWT, present if refreshed.
    public string? RefreshToken { get; set; } // The latest refresh token, present if refreshed.
}
