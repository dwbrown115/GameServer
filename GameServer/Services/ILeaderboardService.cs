using SharedLibrary.Responses;

namespace GameServer.Services
{
    public interface ILeaderboardService
    {
        Task<LeaderboardDataResponse> GetLeaderboardAsync(CancellationToken ct = default);
    }
}
