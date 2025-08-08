namespace SharedLibrary.Responses;

public class SpawnedObjectResponse
{
    public required SpawnedObject SpawnedObject { get; set; }
    public DateTime NextSpawnAllowed { get; set; }
    public required string Message { get; set; }
}

public class SpawnedObject
{
    public required string ObjectId { get; set; }
    public required string ObjectType { get; set; }
    public required Position SpawnPosition { get; set; }
    public required string Status { get; set; } // e.g. "spawned", "claimed"
    public required DateTime SpawnTimestamp { get; set; }
    public required int DecayTimeoutSeconds { get; set; }
    public required float RelevanceScore { get; set; }
}

// {
//   "spawned_object": {
//     "object_id": "obj_1021",
//     "object_type": "gem",
//     "spawn_position": { "x": 41.0, "y": 2.1, "z": -17.5 },
//     "status": "spawned",
//     "spawn_timestamp": "2025-08-05T19:52:12Z",
//     "decay_timeout_seconds": 300,
//     "relevance_score": 0.87
//   },
//   "next_spawn_allowed": "2025-08-05T19:54:00Z",
//   "message": "Object spawned and registered."
// }
