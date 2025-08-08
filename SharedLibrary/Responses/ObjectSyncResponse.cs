using SharedLibrary.Responses;

namespace SharedLibrary.Requests;

public class ObjectSyncResponse
{
    public required List<RemovedObject> RemovedObjects { get; set; }
    public required List<SpawnedObject> NewObjects { get; set; }
}

public class RemovedObject
{
    public required string ObjectId { get; set; }
    public required string Reason { get; set; } // "expired", "low_relevance", "outside_radius"
}

// {
//   "removed_objects": [
//     { "object_id": "obj_1015", "reason": "expired" },
//     { "object_id": "obj_1009", "reason": "low_relevance" }
//   ],
//   "new_objects": [
//     {
//       "object_id": "obj_1023",
//       "object_type": "crate",
//       "spawn_position": { "x": 39.8, "y": 2.0, "z": -19.0 },
//       "status": "spawned",
//       "spawn_timestamp": "2025-08-05T19:52:56Z",
//       "reused": true,
//       "decay_timeout_seconds": 240,
//       "relevance_score": 0.93
//     }
//   ]
// }
