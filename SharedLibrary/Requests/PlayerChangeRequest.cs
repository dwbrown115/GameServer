using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SharedLibrary.Requests;

// This is the class the controller action should expect with [FromBody]
// It is "flat" and does not have a "request" wrapper object.
public class PlayerChangeRequest
{
    [Required]
    public required string UserId { get; set; }
    [Required]
    public required string DeviceId { get; set; }
    [Required]
    public required string RefreshToken { get; set; }
    
    // This object is not required, but if it's present, its contents will be validated.
    public PlayerChangesPayload? Changes { get; set; }
}

public class PlayerChangesPayload
{
    // Make properties nullable so they are optional in the JSON payload.
    // The JSON deserializer will ignore them if they are not present in the request.
    public string? Username { get; set; }
    public PasswordChangePayload? Password { get; set; }
}

public class PasswordChangePayload
{
    [Required]
    public required string OldPassword { get; set; }
    [Required]
    public required string NewPassword { get; set; }
}
