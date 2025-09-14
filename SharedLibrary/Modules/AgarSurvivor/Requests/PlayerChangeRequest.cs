using System.ComponentModel.DataAnnotations;

namespace SharedLibrary.Modules.AgarSurvivor.Requests;

public class PlayerChangeRequest
{
    [Required]
    public required string UserId { get; set; }

    [Required]
    public required string DeviceId { get; set; }

    [Required]
    public required string RefreshToken { get; set; }

    public PlayerChangesPayload? Changes { get; set; }
}

public class PlayerChangesPayload
{
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
