using GameServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SharedLibrary.Models;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace GameServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShopController : ControllerBase
    {
        private readonly GameDbContext _db;
        private readonly ILogger<ShopController> _logger;

        public ShopController(GameDbContext db, ILogger<ShopController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpPost("buy-skin")]
        [ProducesResponseType(typeof(BuySkinResponse), 200)]
        public async Task<IActionResult> BuySkin(
            [FromBody] BuySkinRequest request,
            CancellationToken ct
        )
        {
            if (
                string.IsNullOrWhiteSpace(request.UserId)
                || string.IsNullOrWhiteSpace(request.SkinId)
            )
            {
                return BadRequest(
                    new BuySkinResponse { Approved = false, Message = "Invalid userId or skinId." }
                );
            }

            var userData = await _db.UserDatas.FirstOrDefaultAsync(
                u => u.UserId == request.UserId,
                ct
            );
            if (userData == null)
            {
                // Initialize user data with zero points & empty OwnedSkins if absent
                userData = new UserData
                {
                    UserId = request.UserId,
                    Points = 0,
                    OwnedSkins = JsonConvert.SerializeObject(new List<object>()),
                };
                _db.UserDatas.Add(userData);
                await _db.SaveChangesAsync(ct);
            }

            List<SkinOwnershipEntry> owned;
            if (string.IsNullOrWhiteSpace(userData.OwnedSkins))
            {
                owned = new List<SkinOwnershipEntry>();
            }
            else
            {
                try
                {
                    owned =
                        JsonConvert.DeserializeObject<List<SkinOwnershipEntry>>(
                            userData.OwnedSkins!
                        ) ?? new List<SkinOwnershipEntry>();
                }
                catch
                {
                    owned = new List<SkinOwnershipEntry>();
                }
            }

            if (owned.Any(o => o.SkinId == request.SkinId))
            {
                return Ok(
                    new BuySkinResponse { Approved = false, Message = "Skin already owned." }
                );
            }

            var skin = await _db.Skins.FirstOrDefaultAsync(s => s.UUID == request.SkinId, ct);
            if (skin == null)
            {
                return Ok(new BuySkinResponse { Approved = false, Message = "Skin not found." });
            }

            if (userData.Points < skin.Price)
            {
                return Ok(new BuySkinResponse { Approved = false, Message = "Not enough points." });
            }

            userData.Points -= skin.Price;
            owned.Add(new SkinOwnershipEntry { SkinId = request.SkinId });
            userData.OwnedSkins = JsonConvert.SerializeObject(owned);
            await _db.SaveChangesAsync(ct);

            return Ok(new BuySkinResponse { Approved = true, Message = "Purchase successful." });
        }
    }

    public class SkinOwnershipEntry
    {
        [JsonProperty("SkinId")]
        public string SkinId { get; set; } = string.Empty;
    }
}
