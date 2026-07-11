# Mob body models (3.1.10)

Give a mob a visual body — no networked prefab, no registration, no code. Two ways, both resolved by `MobModel`
on the shared `Enemy` prefab from the mob's `modelId` (its explicit id, or its mob id by convention):

## 1. Catalog (preferred) — reference prefabs in place, no copying
`Assets/Resources/MobModelCatalog.asset` maps `modelId → prefab (+ optional animator controller)`. Prefabs are
referenced **where they already live** (any Synty pack's `Prefabs/Characters/` folder) — nothing is copied here.

- One-click populate: **`Tools/Character/Build Mob Model Catalog`** scans every imported character prefab and
  adds an entry per body (idempotent; re-run after importing a pack).
- Then edit the asset: rename `modelId`s (to a mob id for the convention path, or whatever reads best), and set
  an `animatorController` **only** for non-Humanoid / Generic-rigged bodies. Humanoid bodies animate for free by
  retargeting the shared locomotion controller.

## 2. This folder (zero-setup fallback)
If a `modelId` isn't in the catalog, `MobModel` falls back to `Resources/MobModels/<modelId>.prefab`. Drop a
prefab here named to match the mob id. Simple, but it copies the prefab and ships everything in Resources — the
catalog is better for anything beyond a quick test.

## Requirements / notes
- A body prefab must be a **rigged character prefab** (SkinnedMeshRenderer + Animator + Avatar) — not a raw
  `.fbx` (that's Synty's source atlas of every body) and not a static prop. Synty ships per-character prefabs in
  each pack's `Prefabs/Characters/` — use those, not `Characters.fbx`.
- The `SM_Chr_*` prefix is just Synty's file-naming — nothing in our code requires it. Any prefab, any name, any
  source works.
- Missing model → the placeholder cube stays visible + a console warning (an obvious "art not found" marker).
- The placeholder cube's collider is the click-to-target volume; large bodies overhang it (per-mob target sizing
  is deferred polish).
