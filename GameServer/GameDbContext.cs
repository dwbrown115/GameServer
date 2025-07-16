using Microsoft.EntityFrameworkCore;
using SharedLibrary;

namespace GameServer;

public class GameDbContext : DbContext {
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) {
    }

    public DbSet<User> Users { get; set; }
    // public DbSet<JwtToken> JwtTokens { get; set; }
    // public DbSet<Hero> Heroes { get; set; }
    public DbSet<RefreshTokenRecord> RefreshTokens { get; set; }
}