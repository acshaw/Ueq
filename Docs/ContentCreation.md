# Content Creation Reference

This document covers the data-driven content pipeline for mobs, spawning, and world placement.
Target audience: designers and developers adding new enemies, spawn configurations, and camps.

---

## Table of Contents

1. [Overview — How the Systems Connect](#overview)
2. [Mob Editor](#mob-editor)
3. [Spawn Table (web-authored)](#spawn-table)
4. [Spawn Point](#spawn-point)
5. [World Placement Sync](#world-placement-sync)
6. [End-to-End Workflow](#end-to-end-workflow)
7. [Common Recipes](#common-recipes)

---

## Overview

The content pipeline flows like this:

```
Mob Editor (web) ──► mobs (DB) ──┐
                                  ▼
Spawn Editor (web) ──► spawn_tables (DB, weighted entries + inlined respawn timer)
                                  │
                                  ▼
        SpawnPoint (Unity scene GameObject, references a spawn_table_id or mob_id)
                                  │
                                  ▼
                         Live enemy in world
```

- **Mob** (`mobs` table) — the full description of one mob type (stats, AI, faction, loot, combat fields).
  Authored in the web **Mob Editor**, not a Unity asset (M2.5).
- **Spawn table** (`spawn_tables` + `spawn_table_entries`) — a weighted list of mobs to draw from when
  spawning, plus an inlined respawn timer (base seconds ± variance). Authored in the web **Spawn Editor**
  (M2.7.2).
- **SpawnPoint** — a scene object that manages one physical spawn location: activates when players are
  nearby, rolls its spawn table (or a single named mob), and respawns on death.

**Content vs. placement.** Since 2.7.3, this pipeline splits cleanly into two independent axes:
- **Content** (what a mob *is*, what a spawn table *contains*) — authored in the web editors, backed by
  Postgres, read by the server at startup. Changing content takes effect on a server **restart**.
- **Placement** (*where* a `SpawnPoint`/`PatrolRoute`/`WanderRegion` physically exists in the world) —
  authored in the Unity **Scene view** as always (gizmos, terrain-snap, navmesh sampling), then synced into
  the same database via an Editor tool. See [World Placement Sync](#world-placement-sync) — this is what
  makes adding a *new* spawn location a restart too, not a full rebuild + redeploy.

No code changes are needed to add a new mob type, spawn table, or spawn location.

---

## Mob Editor

> **Authored in the web app, not Unity, since M2.5.** Open the Angular content admin and use the **Mobs**
> tab — it's backed by Postgres (`mobs` table), not a `MobDefinition` ScriptableObject asset. The section
> below describes the original pre-M2.5 Unity `Tools → Mob Editor` workflow; it's kept for historical
> reference (and because the field meanings below still apply 1:1 to the web editor's fields), but the
> actual authoring surface today is the web app.

The Mob Editor is a Unity EditorWindow for creating and editing `MobDefinition` assets.

### Interface

```
┌─────────────────────┬──────────────────────────────────────┐
│  Mob Definitions    │  [Selected mob name]                  │
│  ─────────────────  │  ────────────────────────────────────  │
│  Guard              │  IDENTITY                             │
│  Skeleton           │    Display Name    Guard              │
│  Orc Pawn       ◄── │    Prefab          Enemy.prefab       │
│  Stonefist          │                                       │
│                     │  COMBAT                               │
│                     │    Max Health      100                │
│                     │    Attack Damage   5                  │
│                     │    Attack Interval 2                  │
│                     │    Attack Range    2                  │
│                     │                                       │
│                     │  MOVEMENT                             │
│  ─────────────────  │    Move Speed      3.5                │
│  [New Mob]  Create  │                                       │
│                     │  AI                                   │
│                     │    Perception Radius  20               │
│                     │    Wander Radius      10               │
│                     │    Wander Pause Min   2               │
│                     │    Wander Pause Max   6               │
│                     │    Base Aggro Threat  1               │
│                     │                                       │
│                     │  FACTION                              │
│                     │    Faction            QeynosGuards    │
│                     │    Aggro Max Standing Threatening     │
│                     │    Warning Max Standing Apprehensive  │
│                     │                                       │
│                     │  [Ping Asset]                         │
└─────────────────────┴──────────────────────────────────────┘
```

### Field Reference

#### Identity
| Field | Description |
|---|---|
| Display Name | Name shown on the GameObject in the scene hierarchy. |
| Prefab | The base prefab to instantiate. Use `Enemy.prefab` for all standard mobs. Diverge to a different prefab only when behavior requires it (e.g., a boss with a unique component set). |

#### Combat
| Field | Description |
|---|---|
| Max Health | Hit points this mob spawns with. |
| Attack Damage | Damage dealt per auto-attack. |
| Attack Interval | Seconds between auto-attacks. |
| Attack Range | Distance (metres) at which this mob can strike. |

#### Movement
| Field | Description |
|---|---|
| Move Speed | NavMeshAgent speed. Affects both wander and chase. |

#### AI
| Field | Description |
|---|---|
| Perception Radius | Distance (metres) at which this mob detects players. Drives the `OnPerceived` event. |
| Wander Radius | How far from its spawn point the mob wanders while idle. |
| Wander Pause Min / Max | Random pause duration (seconds) between wander steps. |
| Base Aggro Threat | Threat added to this mob's threat list when it first perceives a KOS player. Higher values make it harder to peel aggro with abilities later. |

#### Faction
| Field | Description |
|---|---|
| Faction | The faction this mob belongs to. Determines how player faction scores are evaluated. |
| Aggro Max Standing | Standings at or below this threshold trigger aggro. Default: `Threatening`. |
| Warning Max Standing | Standings above Aggro Max but at or below this threshold trigger a warning. Default: `Apprehensive`. |

> **Faction standing order (low → high):**
> `KOS → Threatening → Dubious → Apprehensive → Indifferent → Amiable → Kindly → Warmly → Ally`
>
> A player whose score maps to `Threatening` or below will be attacked. A player at `Dubious` or `Apprehensive` gets a warning. `Indifferent` and above are ignored.

---

## Spawn Table

**Authored in:** the web app's **Spawn Editor** tab (backed by `spawn_tables` + `spawn_table_entries` in
Postgres, M2.7.2). The legacy Unity `SpawnTable`/`SpawnTimer` ScriptableObject path still exists as a
deprecated fallback (`SpawnPoint`'s serialized `spawnTable` field) but new content should always go through
the web editor.

A spawn table defines *what* can spawn at a location, how likely each option is, and how long it takes to
respawn. It's separate from the spawn point so the same table can drive multiple locations.

### Fields

| Field | Description |
|---|---|
| `spawn_table_id` | Stable id, referenced by a `SpawnPoint`'s `spawnTableId`. |
| Display name | Human-readable label shown in the editor. |
| Timer base / variance | Respawn delay = `base ± Random(variance)`, clamped to a minimum of 0. |
| Entries | One or more `(mob, weight, group size)` rows. |

### How weighted rolling works

The table sums all entry weights, picks a random number in `[0, total)`, then walks the entries until the
cumulative weight exceeds the roll.

**Example:**

| Mob | Weight | Effective chance |
|---|---|---|
| Orc Pawn | 90 | 90% |
| Orc Captain | 9 | 9% |
| Gruul (named) | 1 | 1% |

This is the classic **placeholder system**: the named mob occupies the same spawn point as the common mob
and appears rarely. No special-case code is needed — it is purely a data decision.

### Group spawning

An entry's `group size` is the number of mobs spawned per activation (M2.7.2, DS3). A `SpawnPoint` tracks
the whole group as a live set and only starts its respawn timer once the *last* member dies — killing part
of a group doesn't trigger an early respawn.

### Sharing tables

Multiple spawn points can reference the same spawn table by id. This is useful for establishing a consistent
population across a camp without duplicating data — edit the table once in the web editor, every point that
references it updates on the next server restart.

---

## Spawn Point

**Component:** `SpawnPoint` (add to any scene GameObject — still authored in Unity; see
[World Placement Sync](#world-placement-sync) for how this now also reaches the database).

A `SpawnPoint` manages a spawn location: activates when a player enters its radius, spawns from its table
(or a single named mob), and schedules respawns when its group dies.

### Inspector fields

| Field | Type | Description |
|---|---|---|
| Spawn Table Id | string | A `spawn_table_id` from the web Spawn Editor — weighted/timed/grouped spawning. Highest precedence. |
| Mob Id | string | A single DB mob id (from the web Mob Editor), used only if Spawn Table Id is empty — for unique/named NPCs (e.g. a Merchant). |
| Activation Radius | float | Distance (metres) within which a player must be present for this point to be active. Default: 50. |
| Patrol Route | PatrolRoute | Optional — if set, spawned mobs patrol this route's ordered waypoints instead of wandering/standing. |
| Wander Region | WanderRegion | Optional (ignored if a Patrol Route is set) — constrains wander mobs to this authored box/sphere area instead of the default leash. |
| Free Range | bool | Optional (ignored if a Wander Region is set) — lets wander mobs roam the whole zone instead of a spawn leash. |
| Snap To Ground | bool | Drop the spawn onto the terrain surface + navmesh so mobs sit on hills instead of at the spawn point's raw Y. |

### Behaviour

```
Player enters radius
        │
        ▼
  Point activates
        │
 group alive? ──Yes──► do nothing
        │
       No
        │
        ▼
  Roll Spawn Table (or resolve Mob Id) → Instantiate group → NetworkServer.Spawn
        │
   Last group member dies
        │
        ▼
  Active? ──No──► wait (timer paused)
        │               │
       Yes         player returns
        │               │
        ▼               ▼
  Start respawn timer (from the table's inlined timer)
        │
  Timer elapses + active?
        │
       Yes ──► Spawn again
        │
        No ──► wait for next activation
```

**Key behaviors:**
- A point that is inactive (no nearby players) does **not** tick its respawn timer. The group spawns
  immediately when a player next enters range.
- The activation check runs every **5 seconds** (server-side poll).
- Mob death is detected via `Health.OnDied` subscriptions — no polling required.
- If neither Spawn Table Id nor Mob Id resolves to anything, the point logs a warning and skips the spawn.

### Scene gizmos

When a spawn point is selected in the Scene view:
- **Yellow ring** — activation radius boundary.
- **Red dot** — exact spawn position.

### Placing spawn points

1. `Tools/Zones/Place Encounter (Spawn Point)` (snaps to the navmesh/ground at the Scene view's focus point), or manually: create an empty GameObject and add the `SpawnPoint` component.
2. Set `Spawn Table Id` to a table authored in the web Spawn Editor (or `Mob Id` for a single named NPC).
3. Optionally wire a Patrol Route / Wander Region / Free Range.
4. Position the GameObject where the mob should appear; verify it's on or near the NavMesh.
5. Set `Activation Radius` to cover the area you want players to "wake up" this spawn from.
6. **Run `Tools/Zones/Sync Placements to Database`** — see below. Without this step the spawn point only
   exists in this scene file; a server running an older build won't have it until you rebuild.

> **Tip:** Spawn points do not need to be on the NavMesh themselves, but the spawn position should be on or very close to it, or the spawned NavMeshAgent will fail to path. Use the NavMesh visualization (`Window → AI → Navigation`) to verify placement.

---

## World Placement Sync

**Added 2.7.3.** Closes the one remaining gap in the DB-backed content pipeline: *content* (mobs, spawn
tables) has been restart-only since M2.5/2.7.2, but *where* a `SpawnPoint`/`PatrolRoute`/`WanderRegion`
physically sits was still baked into the zone's scene file — adding a new camp meant a scene edit **and** a
full Unity rebuild + redeploy. This closes that gap without changing how you place things.

### The model

You keep placing `SpawnPoint`, `PatrolRoute`, and `WanderRegion` in the Editor exactly as before — same
gizmos, same terrain/navmesh snapping, same `Tools/Zones/...` placement tools. Each of these components
implements a small `IWorldPlacement` interface:

- **`PlacementId`** — a GUID assigned once (`OnValidate`, the moment you place the object) and baked into
  the scene from then on. Never hand-edit this field.
- **`CapturePlacementData()` / `ApplyPlacementData()`** — a component's own config as JSON, and the inverse.
  These two methods are the single definition of "what this marker's data means," used identically whether
  the destination is a database row, a live server instance, or a re-imported scene object.

All of it lives in one generic `world_placements` table (`placement_id`, `zone_id`, `marker_type`, position/
rotation, and a `data` JSON blob) — a brand-new marker type invented later needs **zero schema changes**, it
just defines its own JSON shape and a matching factory.

### Exporting: scene → database

`Tools/Zones/Sync Placements to Database` walks every currently open, zone-mapped scene, assigns a
`PlacementId` to anything that doesn't have one yet, and **always upserts** what it finds — no confirmation
needed for creates/updates. It then checks whether the database has any placement for that zone with **no**
matching object in the scene right now, and — only if you confirm — offers to delete those. Nothing is ever
deleted silently; if you're only working with part of a zone open, decline and nothing is lost.

Run this any time you place something new, or after tweaking an existing one, before you consider it "done."

### Materializing: database → any running server

At zone-load time, the server:
1. Indexes every `IWorldPlacement` already baked into the zone's scene (whatever build is currently running).
2. For each DB row matching an object already in the scene: **refreshes its config** from the DB (position
   stays whatever the scene authored — moving something is still a scene-edit-and-rebuild concern, but
   everything else updates on a restart alone, even for placements that were already baked into an older
   build).
3. For each DB row with **no** matching scene object: **materializes** a brand-new one — ephemeral, never
   written to a scene asset, thrown away when the server stops (exactly like a `SpawnPoint`'s own spawned
   mobs).

A `SpawnPoint` that references a `PatrolRoute`/`WanderRegion` resolves that reference correctly regardless
of which side is scene-baked vs. database-materialized (a two-pass load handles the ordering).

**The upshot:** place a new camp, sync it, and any already-running (or freshly restarted) server picks it
up — no rebuild, no redeploy.

### Importing: database → scene (the round trip)

`Tools/Zones/Import Placements from Database` is the reverse: pick a zone (its scene must already be open),
and every DB row for that zone is pulled into the scene as a real, persisted, editable GameObject — an
object already present (matched by `PlacementId`) is refreshed in place, never duplicated. This closes the
loop for anything authored outside Unity (a script against the database, a future admin action): it can
always be pulled in for visual editing in the Scene view, then pushed back out with the sync tool.

**Import fresh before hand-editing something you didn't just place yourself.** There's no version-locking —
exporting a scene copy that predates a since-edited DB row will overwrite that edit, same as every other
content type in this pipeline.

### The web Placement Editor

The **Placements** tab in the web content admin lists every placement across every zone. Position, rotation,
zone, and marker type are always read-only there — a `SpawnPoint`'s non-spatial config (spawn table, mob id,
activation radius, snap-to-ground, free-range) can be edited directly from the web, same as the Spawn
Editor already lets you tune table contents without reopening Unity. `PatrolRoute`/`WanderRegion` rows are
listed for visibility but have no editable fields there — their data is spatial (waypoints, box/sphere
shape), which belongs in the Scene view, not a number form.

---

## End-to-End Workflow

Here is the full flow for adding a new mob type and placing it in the world.

### Step 1 — Define the mob

1. Open the web content admin's **Mobs** tab.
2. Create a new mob, fill in identity/combat/movement/AI/faction fields. At minimum set: prefab address,
   max health, attack damage, and faction.
3. Save.

### Step 2 — Create a spawn table

1. Open the **Spawns** tab.
2. Create a new spawn table, add one or more mob entries with weights, and set the respawn timer
   (base ± variance).

### Step 3 — Place a Spawn Point

1. In Unity, `Tools/Zones/Place Encounter (Spawn Point)` at the desired world position (or hand-place one).
2. Set its `Spawn Table Id` to the id you created in Step 2.
3. Verify the spawn position is on/near the NavMesh.
4. **Run `Tools/Zones/Sync Placements to Database`.** This is the step that makes the spawn point live for
   any server — Editor Play mode, a locally hosted server, or an already-deployed dedicated server on its
   next restart — without a rebuild.
5. Enter Play mode (or wait for the next server restart) — the mob should appear once a player enters the
   activation radius.

---

## Common Recipes

### Single fixed mob (no randomness)

A spawn table with one entry, weight 1. The weight value doesn't matter when there's only one entry — it
always wins the roll.

### Placeholder + named

```
Spawn table "Camp A":
  Orc Pawn       weight 19
  Gruul (named)  weight 1
```

Both entries share the same spawn point. The named mob appears ~5% of the time.

### Multiple spawn points sharing a table

Create one spawn table in the web editor. Set the same `Spawn Table Id` on each spawn point in the camp.
Edit the table's weights/timer once — every point that references it updates on the next server restart.

### Fast-respawn test environment

Set a spawn table's timer to `base = 5, variance = 0` in the web editor while testing. Switch it back to a
real timer before shipping — remember a server restart is all that's needed either way.

### A patrol camp that's fully DB-driven

Place a `PatrolRoute` with waypoints, a `SpawnPoint` referencing it, sync both — the patrol's waypoints and
the spawn point's config both round-trip through the database, and a fresh server (with neither object
baked into its build) will still spawn a patrolling mob correctly.
