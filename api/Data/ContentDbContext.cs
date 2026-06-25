using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Data;

/// <summary>
/// EF Core context for content tables — <b>mapping-only</b>. It maps entities onto tables that
/// already exist (created by Unity's StreamingAssets <c>.sql</c> migration runner). EF Migrations
/// is never enabled and the app never calls <c>Migrate()</c>/<c>EnsureCreated()</c>; the SQL runner
/// stays the single schema authority (devplan D4). When a content type is added, add its DbSet +
/// mapping here to match the migration's columns.
/// </summary>
public class ContentDbContext : DbContext
{
    public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

    public DbSet<ContentPing> ContentPings => Set<ContentPing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Map onto the snake_case columns the migration created. Explicit so EF never guesses
        // (and never tries to own the schema).
        modelBuilder.Entity<ContentPing>(e =>
        {
            e.ToTable("content_ping");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(p => p.Label).HasColumnName("label");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
