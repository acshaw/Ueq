using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// Read-only over <c>mob_models</c> (2026-09-19). Unlike every other content controller, this is not an
/// authoring path — Unity's <c>Tools/Character/Sync Mob Model Catalog to Database</c> Editor tool writes
/// these rows directly to Postgres, the same convention as the World Placement sync tools (2.7.3). This
/// controller exists only so the web Mob Editor can populate its Body Model dropdown; there is
/// deliberately no POST/PUT/DELETE — a model id has no meaningful value authored from a web form.
/// </summary>
[ApiController]
[Route("api/mob-models")]
public class MobModelsController : ControllerBase
{
    readonly ContentDbContext _db;

    public MobModelsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MobModel>>> GetAll() =>
        await _db.MobModels.OrderBy(m => m.ModelId).ToListAsync();
}
