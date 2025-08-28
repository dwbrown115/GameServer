using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLibrary.Responses;

namespace GameServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _service;
        private readonly ILogger<LeaderboardController> _logger;

        public LeaderboardController(
            ILeaderboardService service,
            ILogger<LeaderboardController> logger
        )
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(LeaderboardDataResponse), 200)]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var resp = await _service.GetLeaderboardAsync(ct);
            return Ok(resp);
        }
    }
}
