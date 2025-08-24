using Microsoft.EntityFrameworkCore;
using SharedLibrary.Common; // Changed from SharedLibrary.Pings
using SharedLibrary.Models;

namespace GameServer;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options) { }

    public DbSet<PlayerSessionLog> PlayerSessionLogs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshTokenRecord> RefreshTokens { get; set; }
    public DbSet<ObjectLifecycleLog> ObjectLifecycleLogs { get; set; }
    public DbSet<Leaderboard> Leaderboards { get; set; }
    public DbSet<UserData> UserDatas { get; set; }
    public DbSet<Skins> Skins { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshTokenRecord>().ToTable("RefreshTokenRecord", "auth"); // Added this line
        modelBuilder.Entity<PlayerSessionLog>().OwnsOne(p => p.LastKnownPosition);
        modelBuilder.Entity<ObjectLifecycleLog>().OwnsOne(o => o.Coordinates);
    }

    // public DbSet<JwtToken> JwtTokens { get; set; }
    // public DbSet<Hero> Heroes { get; set; }
}
