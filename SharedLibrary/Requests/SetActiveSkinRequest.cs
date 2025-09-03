namespace SharedLibrary.Requests;

public class SetActiveSkinRequest
{
    public string UserId { get; set; } = string.Empty;
    public string SkinId { get; set; } = string.Empty;
}
