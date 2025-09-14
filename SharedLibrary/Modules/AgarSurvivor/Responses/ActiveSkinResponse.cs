namespace SharedLibrary.Modules.AgarSurvivor.Responses;

public class ActiveSkinResponse
{
    public string Response_Type { get; set; } = "active_skin_response";
    public string UserId { get; set; } = string.Empty;
    public string SkinId { get; set; } = string.Empty;
    public string HexValue { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Ok | Bad
    public string? Message { get; set; }
}
