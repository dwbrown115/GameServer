namespace SharedLibrary.Responses;

public class WebSocketAuthResponse
{
    public required bool Authenticated { get; set; }
    public string? SessionId { get; set; } // Only if authenticated
    public string? Reason { get; set; } // Only if not authenticated
}
