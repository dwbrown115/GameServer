namespace SharedLibrary.Models;

public class ValidatedObject
{
    public required string ObjectId { get; set; }
    public required string SessionId { get; set; }
    // Position
    public Position2D SpawnPosition { get; set; } = new Position2D();

    public DateTime SpawnTimestamp { get; set; }

    // Claim Info
    public bool Claimed { get; set; }
    public required string ClaimedByPlayer { get; set; }
    public DateTime? ClaimTimestamp { get; set; }

    // Context
    public required string Status { get; set; } // "pooled", "spawned", "claimed", "expired"
}

public class Position2D
{
    public float X { get; set; }
    public float Y { get; set; }
}
