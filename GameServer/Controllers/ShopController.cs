using GameServer.Models;
using Microsoft.AspNetCore.Authorization;
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

        [HttpGet("skins")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(SkinsDataResponse), 200)]
        public async Task<IActionResult> GetSkins(CancellationToken ct)
        {
            var skins = await _db
                .Skins.Select(s => new SkinDataItem
                {
                    SkinId = s.UUID,
                    HexValue = s.HexValue,
                    Price = s.Price,
                })
                .ToListAsync(ct);

            var resp = new SkinsDataResponse { Payload = skins };
            return Ok(resp);
        }

        [HttpGet("user-assets/{userId}")]
        [Authorize]
        [ProducesResponseType(typeof(UserSkinsAndPointsResponse), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetUserAssets(
            [FromRoute] string userId,
            CancellationToken ct
        )
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = "Invalid userId" });
            }

            // Enforce row-level security: only the authenticated user can access their own assets
            var tokenUserId = User.FindFirst("sub")?.Value;
            if (tokenUserId == null || tokenUserId != userId)
            {
                return StatusCode(
                    403,
                    new { message = "Forbidden: cannot access other user's assets" }
                );
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UUID == userId, ct);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var userData = await _db.UserDatas.FirstOrDefaultAsync(ud => ud.UserId == userId, ct);
            if (userData == null)
            {
                // Provision user data and attempt to seed with white skin if available
                var whiteSkin = await _db.Skins.FirstOrDefaultAsync(
                    s => s.HexValue == "#FFFFFF",
                    ct
                );
                var ownedSeed = new List<object>();
                string activeSkin;
                if (whiteSkin != null)
                {
                    ownedSeed.Add(new { SkinId = whiteSkin.UUID });
                    activeSkin = whiteSkin.UUID;
                }
                else
                {
                    activeSkin = "#FFFFFF"; // fallback hex literal
                }
                userData = new UserData
                {
                    UserId = userId,
                    Points = 0,
                    OwnedSkins = JsonConvert.SerializeObject(ownedSeed),
                    ActiveSkin = activeSkin,
                };
                _db.UserDatas.Add(userData);
                await _db.SaveChangesAsync(ct);
            }

            List<string> ownedSkinIds;
            if (string.IsNullOrWhiteSpace(userData.OwnedSkins))
            {
                ownedSkinIds = new List<string>();
            }
            else
            {
                try
                {
                    var ownedEntries =
                        JsonConvert.DeserializeObject<List<SkinOwnershipEntry>>(
                            userData.OwnedSkins!
                        ) ?? new List<SkinOwnershipEntry>();
                    ownedSkinIds = ownedEntries.Select(e => e.SkinId).ToList();
                }
                catch
                {
                    ownedSkinIds = new List<string>();
                }
            }

            var response = new UserSkinsAndPointsResponse
            {
                UserId = userId,
                Points = userData.Points,
                OwnedSkinIds = ownedSkinIds,
            };

            return Ok(response);
        }

        [HttpPost("buy-skin")]
        [Authorize]
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

            // Row-level authorization: ensure the JWT subject matches the request's userId
            var tokenUserId = User.FindFirst("sub")?.Value;
            if (tokenUserId == null || tokenUserId != request.UserId)
            {
                return StatusCode(
                    403,
                    new BuySkinResponse
                    {
                        Approved = false,
                        Message = "Forbidden: cannot purchase for another user.",
                    }
                );
            }

            var userData = await _db.UserDatas.FirstOrDefaultAsync(
                u => u.UserId == request.UserId,
                ct
            );
            if (userData == null)
            {
                // Initialize user data and auto-grant white skin if present
                var whiteSkin = await _db.Skins.FirstOrDefaultAsync(
                    s => s.HexValue == "#FFFFFF",
                    ct
                );
                var ownedSeed = new List<object>();
                string activeSkin;
                if (whiteSkin != null)
                {
                    ownedSeed.Add(new { SkinId = whiteSkin.UUID });
                    activeSkin = whiteSkin.UUID;
                }
                else
                {
                    activeSkin = "#FFFFFF";
                }
                userData = new UserData
                {
                    UserId = request.UserId,
                    Points = 0,
                    OwnedSkins = JsonConvert.SerializeObject(ownedSeed),
                    ActiveSkin = activeSkin,
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
                var ownedIdsForResponse = owned.Select(o => o.SkinId).ToList();
                return Ok(
                    new BuySkinResponse
                    {
                        Approved = false,
                        Message = "You do not have enough points to buy this skin",
                        PointsAfterPurchase = userData.Points,
                        OwnedSkinIds = ownedIdsForResponse,
                    }
                );
            }

            // Deduct price & log the points change in PointsLog
            // Parse or init PointsLog
            List<PointsLogEntry> pointsLog;
            if (string.IsNullOrWhiteSpace(userData.PointsLog))
            {
                pointsLog = new List<PointsLogEntry>();
            }
            else
            {
                try
                {
                    pointsLog =
                        JsonConvert.DeserializeObject<List<PointsLogEntry>>(userData.PointsLog!)
                        ?? new List<PointsLogEntry>();
                }
                catch
                {
                    pointsLog = new List<PointsLogEntry>();
                }
            }
            // Snapshot pre-deduction
            pointsLog.Add(
                new PointsLogEntry
                {
                    PointsAtTime = userData.Points,
                    PointsAtTimestamp = DateTime.UtcNow,
                }
            );
            userData.Points -= skin.Price;
            userData.PointsLog = JsonConvert.SerializeObject(pointsLog);

            // Add skin UUID to owned skins array
            owned.Add(new SkinOwnershipEntry { SkinId = skin.UUID });
            userData.OwnedSkins = JsonConvert.SerializeObject(owned);
            await _db.SaveChangesAsync(ct);

            return Ok(
                new BuySkinResponse
                {
                    Approved = true,
                    Message = "Purchase successful.",
                    PointsAfterPurchase = userData.Points,
                    OwnedSkinIds = owned.Select(o => o.SkinId).ToList(),
                }
            );
        }

        [HttpPost("set-active-skin")]
        [Authorize]
        [ProducesResponseType(typeof(ActiveSkinResponse), 200)]
        public async Task<IActionResult> SetActiveSkin(
            [FromBody] SetActiveSkinRequest request,
            CancellationToken ct
        )
        {
            if (
                string.IsNullOrWhiteSpace(request.UserId)
                || string.IsNullOrWhiteSpace(request.SkinId)
            )
            {
                return BadRequest(
                    new ActiveSkinResponse
                    {
                        Status = "Bad",
                        Message = "Invalid userId or skinId",
                        UserId = request.UserId,
                        SkinId = request.SkinId,
                    }
                );
            }
            var tokenUserId = User.FindFirst("sub")?.Value;
            if (tokenUserId == null || tokenUserId != request.UserId)
            {
                return StatusCode(
                    403,
                    new ActiveSkinResponse
                    {
                        Status = "Bad",
                        Message = "Forbidden: cannot set another user's active skin.",
                        UserId = request.UserId,
                        SkinId = request.SkinId,
                    }
                );
            }

            var userData = await _db.UserDatas.FirstOrDefaultAsync(
                u => u.UserId == request.UserId,
                ct
            );
            if (userData == null)
            {
                return NotFound(
                    new ActiveSkinResponse
                    {
                        Status = "Bad",
                        Message = "User data not found",
                        UserId = request.UserId,
                        SkinId = request.SkinId,
                    }
                );
            }

            // Load owned skins
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

            if (!owned.Any(o => o.SkinId == request.SkinId))
            {
                return Ok(
                    new ActiveSkinResponse
                    {
                        Status = "Bad",
                        Message = "Skin not owned",
                        UserId = request.UserId,
                        SkinId = request.SkinId,
                    }
                );
            }

            var skin = await _db.Skins.FirstOrDefaultAsync(s => s.UUID == request.SkinId, ct);
            if (skin == null)
            {
                return Ok(
                    new ActiveSkinResponse
                    {
                        Status = "Bad",
                        Message = "Skin not found",
                        UserId = request.UserId,
                        SkinId = request.SkinId,
                    }
                );
            }

            userData.ActiveSkin = skin.UUID;
            await _db.SaveChangesAsync(ct);

            return Ok(
                new ActiveSkinResponse
                {
                    Status = "Ok",
                    Message = "Active skin set",
                    UserId = request.UserId,
                    SkinId = skin.UUID,
                    HexValue = skin.HexValue,
                }
            );
        }

        [HttpGet("active-skin/{userId}")]
        [Authorize]
        [ProducesResponseType(typeof(ActiveSkinResponse), 200)]
        public async Task<IActionResult> GetActiveSkin(
            [FromRoute] string userId,
            CancellationToken ct
        )
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(
                    new ActiveSkinResponse { Status = "Bad", Message = "Invalid userId" }
                );
            }

            var tokenUserId = User.FindFirst("sub")?.Value;
            if (tokenUserId == null || tokenUserId != userId)
            {
                return StatusCode(
                    403,
                    new ActiveSkinResponse
                    {
                        Status = "Bad",
                        Message = "Forbidden: cannot view another user's active skin.",
                    }
                );
            }

            var userData = await _db.UserDatas.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (userData == null)
            {
                return NotFound(
                    new ActiveSkinResponse { Status = "Bad", Message = "User data not found" }
                );
            }

            string? skinId = userData.ActiveSkin;
            string hex = "#FFFFFF"; // default white
            if (!string.IsNullOrWhiteSpace(skinId) && skinId != "#FFFFFF")
            {
                var skin = await _db.Skins.FirstOrDefaultAsync(s => s.UUID == skinId, ct);
                if (skin != null)
                {
                    hex = skin.HexValue;
                }
            }

            return Ok(
                new ActiveSkinResponse
                {
                    Status = "Ok",
                    UserId = userId,
                    SkinId = skinId ?? "#FFFFFF",
                    HexValue = hex,
                }
            );
        }
    }

    public class SkinOwnershipEntry
    {
        [JsonProperty("SkinId")]
        public string SkinId { get; set; } = string.Empty;
    }
}
