using GameServer.Models;
using GameServer.Services;
using GameServer.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SharedLibrary.Modules.AgarSurvivor.Requests;
using SharedLibrary.Modules.AgarSurvivor.Responses;

namespace GameServer.GameModules.AgarSurvivor;

public static class AgarSurvivorEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // Leaderboard GET
        group
            .MapGet(
                "/Leaderboard",
                async ([FromServices] ILeaderboardService svc, CancellationToken ct) =>
                {
                    var resp = await svc.GetLeaderboardAsync(ct);
                    return Results.Ok(resp);
                }
            )
            .AllowAnonymous();

        // Shop endpoints (migrate from ShopController)
        group
            .MapGet(
                "/Shop/skins",
                async ([FromServices] GameDbContext db, CancellationToken ct) =>
                {
                    var skins = await db
                        .Skins.Select(s => new SkinDataItem
                        {
                            SkinId = s.UUID,
                            HexValue = s.HexValue,
                            Price = s.Price,
                        })
                        .ToListAsync(ct);
                    return Results.Ok(new SkinsDataResponse { Payload = skins });
                }
            )
            .AllowAnonymous();

        group
            .MapGet(
                "/Shop/user-assets/{userId}",
                async (
                    HttpContext http,
                    [FromRoute] string userId,
                    [FromServices] GameDbContext db,
                    CancellationToken ct
                ) =>
                {
                    if (string.IsNullOrWhiteSpace(userId))
                        return Results.BadRequest(new { message = "Invalid userId" });
                    var tokenUserId = http.User.GetUserId();
                    if (tokenUserId == null || tokenUserId != userId)
                        return Results.Json(
                            new { message = "Forbidden: cannot access other user's assets" },
                            statusCode: 403
                        );

                    var user = await db.Users.FirstOrDefaultAsync(u => u.UUID == userId, ct);
                    if (user == null)
                        return Results.NotFound(new { message = "User not found" });

                    var userData = await db.UserDatas.FirstOrDefaultAsync(
                        ud => ud.UserId == userId,
                        ct
                    );
                    if (userData == null)
                    {
                        var whiteSkin = await db.Skins.FirstOrDefaultAsync(
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
                        userData = new SharedLibrary.Models.UserData
                        {
                            UserId = userId,
                            Points = 0,
                            OwnedSkins = JsonConvert.SerializeObject(ownedSeed),
                            ActiveSkin = activeSkin,
                        };
                        db.UserDatas.Add(userData);
                        await db.SaveChangesAsync(ct);
                    }

                    List<string> ownedSkinIds;
                    if (string.IsNullOrWhiteSpace(userData.OwnedSkins))
                        ownedSkinIds = new();
                    else
                    {
                        try
                        {
                            var ownedEntries =
                                JsonConvert.DeserializeObject<List<SkinOwnershipEntry>>(
                                    userData.OwnedSkins!
                                ) ?? new();
                            ownedSkinIds = ownedEntries.Select(e => e.SkinId).ToList();
                        }
                        catch
                        {
                            ownedSkinIds = new();
                        }
                    }

                    return Results.Ok(
                        new UserSkinsAndPointsResponse
                        {
                            UserId = userId,
                            Points = userData.Points,
                            OwnedSkinIds = ownedSkinIds,
                        }
                    );
                }
            )
            .RequireAuthorization();

        group
            .MapPost(
                "/Shop/buy-skin",
                async (
                    HttpContext http,
                    [FromBody] BuySkinRequest request,
                    [FromServices] GameDbContext db,
                    CancellationToken ct
                ) =>
                {
                    if (
                        string.IsNullOrWhiteSpace(request.UserId)
                        || string.IsNullOrWhiteSpace(request.SkinId)
                    )
                        return Results.BadRequest(
                            new BuySkinResponse
                            {
                                Approved = false,
                                Message = "Invalid userId or skinId.",
                            }
                        );

                    var tokenUserId = http.User.GetUserId();
                    if (tokenUserId == null || tokenUserId != request.UserId)
                        return Results.Json(
                            new BuySkinResponse
                            {
                                Approved = false,
                                Message = "Forbidden: cannot purchase for another user.",
                            },
                            statusCode: 403
                        );

                    var userData = await db.UserDatas.FirstOrDefaultAsync(
                        u => u.UserId == request.UserId,
                        ct
                    );
                    if (userData == null)
                    {
                        var whiteSkin = await db.Skins.FirstOrDefaultAsync(
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
                        userData = new SharedLibrary.Models.UserData
                        {
                            UserId = request.UserId,
                            Points = 0,
                            OwnedSkins = JsonConvert.SerializeObject(ownedSeed),
                            ActiveSkin = activeSkin,
                        };
                        db.UserDatas.Add(userData);
                        await db.SaveChangesAsync(ct);
                    }

                    List<SkinOwnershipEntry> owned;
                    if (string.IsNullOrWhiteSpace(userData.OwnedSkins))
                        owned = new();
                    else
                    {
                        owned =
                            JsonConvert.DeserializeObject<List<SkinOwnershipEntry>>(
                                userData.OwnedSkins!
                            ) ?? new();
                    }

                    if (owned.Any(o => o.SkinId == request.SkinId))
                        return Results.Ok(
                            new BuySkinResponse
                            {
                                Approved = false,
                                Message = "Skin already owned.",
                            }
                        );

                    var skin = await db.Skins.FirstOrDefaultAsync(
                        s => s.UUID == request.SkinId,
                        ct
                    );
                    if (skin == null)
                        return Results.Ok(
                            new BuySkinResponse { Approved = false, Message = "Skin not found." }
                        );

                    if (userData.Points < skin.Price)
                        return Results.Ok(
                            new BuySkinResponse
                            {
                                Approved = false,
                                Message = "You do not have enough points to buy this skin",
                                PointsAfterPurchase = userData.Points,
                                OwnedSkinIds = owned.Select(o => o.SkinId).ToList(),
                            }
                        );

                    List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry> pointsLog =
                        string.IsNullOrWhiteSpace(userData.PointsLog)
                            ? new()
                            : (
                                JsonConvert.DeserializeObject<
                                    List<SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry>
                                >(userData.PointsLog!) ?? new()
                            );
                    pointsLog.Add(
                        new SharedLibrary.Modules.AgarSurvivor.Models.PointsLogEntry
                        {
                            PointsAtTime = userData.Points,
                            PointsAtTimestamp = DateTime.UtcNow,
                        }
                    );
                    userData.Points -= skin.Price;
                    userData.PointsLog = JsonConvert.SerializeObject(pointsLog);
                    owned.Add(new SkinOwnershipEntry { SkinId = skin.UUID });
                    userData.OwnedSkins = JsonConvert.SerializeObject(owned);
                    await db.SaveChangesAsync(ct);

                    return Results.Ok(
                        new BuySkinResponse
                        {
                            Approved = true,
                            Message = "Purchase successful.",
                            PointsAfterPurchase = userData.Points,
                            OwnedSkinIds = owned.Select(o => o.SkinId).ToList(),
                        }
                    );
                }
            )
            .RequireAuthorization();

        group
            .MapPut(
                "/Shop/active-skin",
                async (
                    HttpContext http,
                    [FromBody] SetActiveSkinRequest request,
                    [FromServices] GameDbContext db,
                    CancellationToken ct
                ) =>
                {
                    if (
                        string.IsNullOrWhiteSpace(request.UserId)
                        || string.IsNullOrWhiteSpace(request.SkinId)
                    )
                        return Results.BadRequest(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "Invalid userId or skinId",
                                UserId = request.UserId,
                                SkinId = request.SkinId,
                            }
                        );
                    var tokenUserId = http.User.GetUserId();
                    if (tokenUserId == null || tokenUserId != request.UserId)
                        return Results.Json(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "Forbidden: cannot set another user's active skin.",
                                UserId = request.UserId,
                                SkinId = request.SkinId,
                            },
                            statusCode: 403
                        );

                    var userData = await db.UserDatas.FirstOrDefaultAsync(
                        u => u.UserId == request.UserId,
                        ct
                    );
                    if (userData == null)
                        return Results.NotFound(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "User data not found",
                                UserId = request.UserId,
                                SkinId = request.SkinId,
                            }
                        );

                    List<SkinOwnershipEntry> owned = string.IsNullOrWhiteSpace(userData.OwnedSkins)
                        ? new()
                        : (
                            JsonConvert.DeserializeObject<List<SkinOwnershipEntry>>(
                                userData.OwnedSkins!
                            ) ?? new()
                        );
                    if (!owned.Any(o => o.SkinId == request.SkinId))
                        return Results.Ok(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "Skin not owned",
                                UserId = request.UserId,
                                SkinId = request.SkinId,
                            }
                        );

                    var skin = await db.Skins.FirstOrDefaultAsync(
                        s => s.UUID == request.SkinId,
                        ct
                    );
                    if (skin == null)
                        return Results.Ok(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "Skin not found",
                                UserId = request.UserId,
                                SkinId = request.SkinId,
                            }
                        );

                    userData.ActiveSkin = skin.UUID;
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(
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
            )
            .RequireAuthorization();

        group
            .MapGet(
                "/Shop/active-skin/{userId}",
                async (
                    HttpContext http,
                    [FromRoute] string userId,
                    [FromServices] GameDbContext db,
                    CancellationToken ct
                ) =>
                {
                    if (string.IsNullOrWhiteSpace(userId))
                        return Results.BadRequest(
                            new ActiveSkinResponse { Status = "Bad", Message = "Invalid userId" }
                        );
                    var tokenUserId = http.User.GetUserId();
                    if (tokenUserId == null || tokenUserId != userId)
                        return Results.Json(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "Forbidden: cannot view another user's active skin.",
                            },
                            statusCode: 403
                        );

                    var userData = await db.UserDatas.FirstOrDefaultAsync(
                        u => u.UserId == userId,
                        ct
                    );
                    if (userData == null)
                        return Results.NotFound(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "User data not found",
                            }
                        );

                    string? skinId = userData.ActiveSkin;
                    string hex = "#FFFFFF";
                    if (!string.IsNullOrWhiteSpace(skinId) && skinId != "#FFFFFF")
                    {
                        var skin = await db.Skins.FirstOrDefaultAsync(s => s.UUID == skinId, ct);
                        if (skin != null)
                            hex = skin.HexValue;
                    }
                    return Results.Ok(
                        new ActiveSkinResponse
                        {
                            Status = "Ok",
                            UserId = userId,
                            SkinId = skinId ?? "#FFFFFF",
                            HexValue = hex,
                        }
                    );
                }
            )
            .RequireAuthorization();

        group
            .MapGet(
                "/Shop/active-skin",
                async (HttpContext http, [FromServices] GameDbContext db, CancellationToken ct) =>
                {
                    var tokenUserId = http.User.GetUserId();
                    if (string.IsNullOrWhiteSpace(tokenUserId))
                        return Results.Json(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "Missing subject claim",
                            },
                            statusCode: 401
                        );

                    var userData = await db.UserDatas.FirstOrDefaultAsync(
                        u => u.UserId == tokenUserId,
                        ct
                    );
                    if (userData == null)
                        return Results.NotFound(
                            new ActiveSkinResponse
                            {
                                Status = "Bad",
                                Message = "User data not found",
                            }
                        );

                    string? skinId = userData.ActiveSkin;
                    string hex = "#FFFFFF";
                    if (!string.IsNullOrWhiteSpace(skinId) && skinId != "#FFFFFF")
                    {
                        var skin = await db.Skins.FirstOrDefaultAsync(s => s.UUID == skinId, ct);
                        if (skin != null)
                            hex = skin.HexValue;
                    }
                    return Results.Ok(
                        new ActiveSkinResponse
                        {
                            Status = "Ok",
                            UserId = tokenUserId,
                            SkinId = skinId ?? "#FFFFFF",
                            HexValue = hex,
                        }
                    );
                }
            )
            .RequireAuthorization();

        // Player endpoints
        app.MapGroup("/player")
            .MapGet(
                "/{id}",
                async ([FromRoute] string id, [FromServices] IPlayerService playerSvc) =>
                {
                    var playerResponse = await playerSvc.GetPlayerAsync(id);
                    return playerResponse == null
                        ? Results.NotFound(new { message = $"Player with ID '{id}' not found." })
                        : Results.Ok(playerResponse);
                }
            );

        app.MapGroup("/player")
            .MapPatch(
                "/update",
                async (
                    [FromBody] PlayerChangeRequest request,
                    [FromServices] IPlayerService playerSvc
                ) =>
                {
                    var changeResponse = await playerSvc.UpdatePlayerDataAsync(request);
                    if (!changeResponse.Success)
                    {
                        if (
                            changeResponse.Message.Contains("session")
                            || changeResponse.Message.Contains("token")
                        )
                            return Results.Unauthorized();
                        return Results.BadRequest(new { message = changeResponse.Message });
                    }
                    return Results.Ok(changeResponse);
                }
            );

        app.MapGroup("/player")
            .MapPost(
                "/object-claimed",
                async (
                    [FromBody] ObjectClaimedRequest request,
                    [FromServices] IPlayerService playerSvc
                ) =>
                {
                    var response = await playerSvc.ObjectClaimedAsync(request);
                    return response.Status != "Ok"
                        ? Results.BadRequest(response)
                        : Results.Ok(response);
                }
            );
    }

    private class SkinOwnershipEntry
    {
        [JsonProperty("SkinId")]
        public string SkinId { get; set; } = string.Empty;
    }
}
