namespace SharedLibrary.Models;

public class PlayerSessionLog
{
    public int Id { get; set; }
    public required string SessionId { get; set; }
    public required string PlayerId { get; set; }
    public DateTime SessionStart { get; set; }
    public DateTime? SessionEnd { get; set; }
    public DateTime? DeletionDate { get; set; }

    // Object Integrity & Sync
    public int? ClientObjCount { get; set; }
    public int? ServerObjCount { get; set; }
    public required string ObjectSyncHash { get; set; }
    public bool? HashMismatch { get; set; }
    public required string SyncStatus { get; set; }
    public required string DesyncResolution { get; set; }
    public bool? RadiusEnforced { get; set; }

    // Object Lifecycle Logging 🔍
    public required string ObjectLifecycleLog { get; set; } // JSON stored as string

    // Scoring & Validation
    public int ScoreServer { get; set; } = 0;
    public int AttemptedClientScore { get; set; } = 0;
    public bool? FakeObjectDetected { get; set; }
    public int? PickupEventsVerified { get; set; }

    // Object Spawn Control
    public int? SpawnRequests { get; set; }
    public int? ValidatedSpawns { get; set; }
    public int? BlockedSpawns { get; set; }
    public bool? SpawnRateFlagged { get; set; }

    // Context Metadata
    public required string SessionMetadata { get; set; } // JSON stored as string
    public required string Region { get; set; }
    public required string GameVersion { get; set; }
    public required string Platform { get; set; }

    // Audit & Notes
    public bool? FlaggedForReview { get; set; }
    public required string AdminNotes { get; set; }
}
