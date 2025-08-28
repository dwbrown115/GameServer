using GameServer.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Responses;

namespace GameServer.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly GameDbContext _db;
        private readonly ILogger<LeaderboardService> _logger;

        public LeaderboardService(GameDbContext db, ILogger<LeaderboardService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<LeaderboardDataResponse> GetLeaderboardAsync(
            CancellationToken ct = default
        )
        {
            var items = await _db
                .Leaderboards.OrderByDescending(l => l.PlayerHighestScore)
                .Select(l => new LeaderboardDataItem
                {
                    Username = l.Username,
                    PlayerHighestScore = l.PlayerHighestScore,
                })
                .ToListAsync(ct);
            _logger.LogInformation("Fetched {Count} leaderboard entries", items.Count);
            return new LeaderboardDataResponse { Payload = items };
        }
    }
}
