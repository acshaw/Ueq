namespace Ueq.ContentApi.Models;

/// <summary>
/// EF entity mapping onto the <c>mob_models</c> table (2026-09-19). A pure lookup of the model ids known
/// to Unity's <c>MobModelCatalog</c> — kept in sync by the Editor tool
/// <c>Tools/Character/Sync Mob Model Catalog to Database</c>, never written from the web. Exists only so
/// the Mob Editor can offer a dropdown instead of requiring the exact catalog modelId string to be typed
/// by hand.
/// </summary>
public class MobModel
{
    public string ModelId { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
