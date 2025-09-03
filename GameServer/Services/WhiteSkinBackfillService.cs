using GameServer.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SharedLibrary.Models;

namespace GameServer.Services;

/// <summary>
/// One-time startup backfill that ensures any existing gameplay.UserData rows
/// own the canonical white skin (#FFFFFF) if it exists and sets ActiveSkin to that
/// skin UUID when their ActiveSkin is currently null or the literal fallback hex.
/// Idempotent: safe to run multiple times (no duplicate ownership entries).
/// Controlled by Settings.RunWhiteSkinBackfill (default false/null => skip).
/// </summary>
public class WhiteSkinBackfillService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<WhiteSkinBackfillService> _logger;
    private readonly Settings _settings;

    public WhiteSkinBackfillService(
        IServiceProvider sp,
        ILogger<WhiteSkinBackfillService> logger,
        Settings settings
    )
    {
        _sp = sp;
        _logger = logger;
        _settings = settings;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!(_settings.RunWhiteSkinBackfill ?? false))
        {
            _logger.LogInformation(
                "WhiteSkinBackfillService disabled (RunWhiteSkinBackfill=false). Skipping."
            );
            return;
        }

        _logger.LogInformation("WhiteSkinBackfillService starting backfill...");
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var whiteSkin = await db.Skins.FirstOrDefaultAsync(
                s => s.HexValue == "#FFFFFF",
                cancellationToken
            );
            if (whiteSkin == null)
            {
                _logger.LogWarning("No #FFFFFF skin found. Backfill exiting with no changes.");
                return;
            }

            var userDatas = await db.UserDatas.ToListAsync(cancellationToken);
            int updated = 0;
            foreach (var ud in userDatas)
            {
                // Parse owned skins list
                List<WhiteSkinOwnership> ownedEntries = new();
                if (!string.IsNullOrWhiteSpace(ud.OwnedSkins))
                {
                    try
                    {
                        ownedEntries =
                            JsonConvert.DeserializeObject<List<WhiteSkinOwnership>>(ud.OwnedSkins!)
                            ?? new List<WhiteSkinOwnership>();
                    }
                    catch
                    {
                        ownedEntries = new List<WhiteSkinOwnership>();
                    }
                }
                bool hadWhite = ownedEntries.Any(e => e.SkinId == whiteSkin.UUID);
                if (!hadWhite)
                {
                    ownedEntries.Add(new WhiteSkinOwnership { SkinId = whiteSkin.UUID });
                }

                bool activeNeedsUpdate =
                    string.IsNullOrWhiteSpace(ud.ActiveSkin) || ud.ActiveSkin == "#FFFFFF";
                if (!hadWhite || activeNeedsUpdate)
                {
                    ud.OwnedSkins = JsonConvert.SerializeObject(ownedEntries);
                    if (activeNeedsUpdate)
                        ud.ActiveSkin = whiteSkin.UUID;
                    updated++;
                }
            }

            if (updated > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            _logger.LogInformation(
                "WhiteSkinBackfill complete. {Updated} UserData rows updated.",
                updated
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhiteSkinBackfillService encountered an error.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private class WhiteSkinOwnership
    {
        public string SkinId { get; set; } = string.Empty;
    }
}
