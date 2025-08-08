namespace SharedLibrary.Responses;

public class PlayerPositionPing
{
    public required string SessionId { get; set; }
    public required string PlayerId { get; set; }
    public required Position CurrentPosition { get; set; }
    public float Radius { get; set; }
    public DateTime LastSpawnAttempt { get; set; }
}

public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
}

// POST: /api/player/position
// {
//   "session_id": "abc123",
//   "player_id": "dakota_01",
//   "current_position": {
//     "x": 40.3,
//     "y": 2.0,
//     "z": -18.7
//   },
//   "radius": 50.0,
//   "last_spawn_attempt": "2025-08-05T19:51:02Z"
// }
