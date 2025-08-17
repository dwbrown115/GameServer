namespace GameServer.Models;

public class Settings
{
    public string? BearerKey { get; set; }
    public string? JwtSecret { get; set; }
    public float? SpawnCooldownSeconds { get; set; }
    public float? NoSpawnRadius { get; set; }
}
