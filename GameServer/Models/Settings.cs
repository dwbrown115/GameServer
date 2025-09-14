namespace GameServer.Models;

public class Settings
{
    public string? BearerKey { get; set; }
    public required string JwtSecret { get; set; }
    public float? SpawnCooldownSeconds { get; set; }
    public float? NoSpawnRadius { get; set; }

    // When true, a one-time startup hosted service will backfill the white skin ownership & active skin UUID for existing users.
    public bool? RunWhiteSkinBackfill { get; set; }

    // Future: choose which game module is active
    public string? ActiveGameModule { get; set; }
}
