namespace Ueq.ContentApi.Models;

/// <summary>
/// EF entity mapping onto the <c>content_ping</c> table (2.1 smoke type). The table is
/// created by Unity's StreamingAssets migration runner — this class only maps onto it.
/// EF Migrations is deliberately NOT used (the SQL runner is the single schema authority, D4),
/// so this entity is hand-maintained to match the migration's columns.
/// </summary>
public class ContentPing
{
    public long Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
