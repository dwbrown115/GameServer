using SharedLibrary.Responses;

namespace SharedLibrary.Templates;

public class PooledObject
{
    public required string ObjectId { get; set; }
    public required string ObjectType { get; set; }
    public required Position SpawnPosition { get; set; }
    public DateTime SpawnTimestamp { get; set; }
    public bool Claimed { get; set; }
    public required string ClaimedByPlayer { get; set; }
    public DateTime? ClaimTimestamp { get; set; }
    public required string Status { get; set; } // "pooled", "spawned", "claimed", "expired"
    public int DecayTimeoutSeconds { get; set; }
    public float RelevanceScore { get; set; }
}
