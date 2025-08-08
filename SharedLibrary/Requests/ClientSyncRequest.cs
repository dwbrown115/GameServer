using SharedLibrary.Responses;

namespace SharedLibrary.Requests;

public class ClientObjectSyncRequest
{
    public required string SessionId { get; set; }
    public required string PlayerId { get; set; }
    public required Position PlayerPosition { get; set; }
    public float Radius { get; set; }
    public required List<ClientKnownObject> KnownObjects { get; set; }
}

public class ClientKnownObject
{
    public required string ObjectId { get; set; }
    public required string Status { get; set; } // "spawned", "claimed", "expired"
    public float RelevanceScore { get; set; }
}

// POST: /api/objects/sync-client
// {
//   "session_id": "abc123",
//   "player_id": "dakota_01",
//   "player_position": {
//     "x": 40.3,
//     "y": 2.0,
//     "z": -18.7
//   },
//   "radius": 50.0,
//   "known_objects": [
//     { "object_id": "obj_1021", "status": "spawned", "relevance_score": 0.87 },
//     { "object_id": "obj_1018", "status": "claimed", "relevance_score": 0.32 }
//   ]
// }
