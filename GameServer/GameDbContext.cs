using Microsoft.EntityFrameworkCore;
using SharedLibrary.Models;
using SharedLibrary.Common; // Changed from SharedLibrary.Pings

namespace GameServer;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options) { }

    public DbSet<PlayerSessionLog> PlayerSessionLogs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshTokenRecord> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerSessionLog>().OwnsOne(p => p.LastKnownPosition);
    }

    // public DbSet<JwtToken> JwtTokens { get; set; }
    // public DbSet<Hero> Heroes { get; set; }
}
