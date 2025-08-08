namespace SharedLibrary.Results;

public class JwtValidationResult
{
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public bool ShouldRefresh { get; set; }
}
