using Microsoft.EntityFrameworkCore;
using SharedLibrary.Models;

public class MigrationOnlyContext : DbContext
{
    public DbSet<PlayerSessionLog> PlayerSessionLogs { get; set; }

    public MigrationOnlyContext(DbContextOptions<MigrationOnlyContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerSessionLog>().ToTable("PlayerSessionLog", "gameplay");
    }
}
