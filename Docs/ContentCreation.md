# Content Creation Reference

This document covers the data-driven content pipeline for mobs and spawning.
Target audience: designers and developers adding new enemies and spawn configurations.

---

## Table of Contents

1. [Overview — How the Systems Connect](#overview)
2. [Mob Editor](#mob-editor)
3. [Spawn Timer](#spawn-timer)
4. [Spawn Table](#spawn-table)
5. [Spawn Point](#spawn-point)
6. [End-to-End Workflow](#end-to-end-workflow)
7. [Common Recipes](#common-recipes)

---

## Overview

The content pipeline flows like this:

```
MobDefinition  ──►  SpawnTable  ──►  SpawnPoint  ──►  Live enemy in world
     ▲                   ▲
  Mob Editor         SpawnTimer
```

- **MobDefinition** — the full description of one mob type (stats, AI, faction).
- **SpawnTable** — a weighted list of MobDefinitions to draw from when spawning.
- **SpawnTimer** — a reusable respawn delay with randomized variance.
- **SpawnPoint** — a scene object that manages a single spawn location (activates when players are nearby, respawns on death).

All four are ScriptableObject assets (or MonoBehaviour scene objects in the case of SpawnPoint). No code changes are needed to add new mob types or spawn configurations.

---

## Mob Editor

**How to open:** `Tools → Mob Editor`

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
│                     │    Perception Radius  20              │
│                     │    Wander Radius      10              │
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

### Creating a new mob

1. Type a name in the text field at the bottom-left of the window.
2. Click **Create New**.
3. The asset is saved to `Assets/ScriptableObjects/Mobs/<Name>.asset`.
4. Fill in the fields in the right panel. Changes are saved automatically.

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
| Faction | The `FactionDefinition` asset this mob belongs to. Determines how player faction scores are evaluated. |
| Aggro Max Standing | Standings at or below this threshold trigger aggro. Default: `Threatening`. |
| Warning Max Standing | Standings above Aggro Max but at or below this threshold trigger a warning. Default: `Apprehensive`. |

> **Faction standing order (low → high):**
> `KOS → Threatening → Dubious → Apprehensive → Indifferent → Amiable → Kindly → Warmly → Ally`
>
> A player whose score maps to `Threatening` or below will be attacked. A player at `Dubious` or `Apprehensive` gets a warning. `Indifferent` and above are ignored.

---

## Spawn Timer

**Asset path:** `Create → Ueq → Spawn Timer`

A `SpawnTimer` encapsulates a respawn delay as a reusable asset. Multiple spawn points can reference the same timer, so adjusting one asset changes every spawn point that uses it.

### Fields

| Field | Type | Description |
|---|---|---|
| Base Seconds | float | The centre point of the respawn delay in seconds. |
| Variance | float | Maximum random deviation from the base. The actual delay is `baseSeconds + Random(-variance, +variance)`. |

### Suggested presets

| Name | Base | Variance | Use case |
|---|---|---|---|
| Standard | 300s (5 min) | 60s | Typical overworld mobs |
| Fast | 60s (1 min) | 15s | Instanced or low-stakes content |
| Named | 900s (15 min) | 120s | Named / mini-boss mobs |
| Rare | 3600s (1 hr) | 300s | Rare named with contested camps |

### How `Roll()` works

```
actualDelay = baseSeconds + Random.Range(-variance, +variance)
```

The result is clamped to a minimum of 0. If no `SpawnTimer` is assigned to a spawn point (and the spawn table has no default), the spawn point falls back to **300 seconds**.

---

## Spawn Table

**Asset path:** `Create → Ueq → Spawn Table`

A `SpawnTable` defines *what* can spawn at a location and how likely each option is. It is separate from the spawn point so the same table can drive multiple locations.

### Fields

| Field | Type | Description |
|---|---|---|
| Entries | List | One or more `SpawnTableEntry` items (see below). |
| Default Timer | SpawnTimer | Fallback timer used by any spawn point that references this table but has no timer override set. |

### SpawnTableEntry fields

| Field | Type | Description |
|---|---|---|
| Mob | MobDefinition | The mob type this entry produces. |
| Weight | int | Relative probability of this entry being selected. |
| Group Size | int | Reserved for future group spawning. Leave at 1. |

### How weighted rolling works

The table sums all weights, picks a random number in `[0, total)`, then walks the entries until the cumulative weight exceeds the roll.

**Example:**

| Mob | Weight | Effective chance |
|---|---|---|
| Orc Pawn | 90 | 90% |
| Orc Captain | 9 | 9% |
| Gruul (named) | 1 | 1% |

This is the classic **placeholder system**: the named mob occupies the same spawn point as the common mob and appears rarely. No special-case code is needed — it is purely a data decision.

### Sharing tables

Multiple spawn points can reference the same `SpawnTable`. This is useful for establishing a consistent population across a camp without duplicating data.

---

## Spawn Point

**Component:** `SpawnPoint` (add to any scene GameObject)

A `SpawnPoint` manages one live mob at a specific world location. It activates when a player enters its radius, spawns from its table, and schedules respawns when the mob dies.

### Inspector fields

| Field | Type | Description |
|---|---|---|
| Spawn Table | SpawnTable | Determines what spawns here. Required. |
| Timer Override | SpawnTimer | If set, overrides the table's `defaultTimer` for this point only. |
| Activation Radius | float | Distance (metres) within which a player must be present for this point to be active. Default: 50. |

### Behaviour

```
Player enters radius
        │
        ▼
  Point activates
        │
   mob alive? ──Yes──► do nothing
        │
       No
        │
        ▼
  Roll SpawnTable → Instantiate → Inject MobDefinition → NetworkServer.Spawn
        │
   Mob dies
        │
        ▼
  Active? ──No──► wait (timer paused)
        │               │
       Yes         player returns
        │               │
        ▼               ▼
  Start respawn timer
        │
  Timer elapses + active?
        │
       Yes ──► Spawn again
        │
        No ──► wait for next activation
```

**Key behaviors:**
- A point that is inactive (no nearby players) does **not** tick its respawn timer. The mob spawns immediately when a player next enters range.
- The activation check runs every **5 seconds** (server-side poll).
- Mob death is detected via a `Health.OnDied` subscription — no polling required.
- If the spawn table or mob definition's prefab field is empty, the point logs a warning and skips the spawn.

### Scene gizmos

When a spawn point is selected in the Scene view:
- **Yellow ring** — activation radius boundary.
- **Red dot** — exact spawn position.

### Placing spawn points

1. Create an empty GameObject in the Hierarchy.
2. Add the `SpawnPoint` component.
3. Assign a `SpawnTable`.
4. Optionally assign a `SpawnTimer` override.
5. Position the GameObject where the mob should appear.
6. Set `Activation Radius` to cover the area you want players to "wake up" this spawn from.

> **Tip:** Spawn points do not need to be on the NavMesh themselves, but the spawn position should be on or very close to it, or the spawned NavMeshAgent will fail to path. Use the NavMesh visualization (`Window → AI → Navigation`) to verify placement.

---

## End-to-End Workflow

Here is the full flow for adding a new mob type and placing it in the world.

### Step 1 — Define the mob

1. Open `Tools → Mob Editor`.
2. Type the mob's name and click **Create New**.
3. Fill in all sections. At minimum set: **Prefab**, **Max Health**, **Attack Damage**, and **Faction**.
4. The asset saves automatically.

### Step 2 — Create or choose a Spawn Timer

If this mob uses standard timing, assign the shared **Standard** timer. For a named mob, create a new `SpawnTimer` asset with appropriate values via `Create → Ueq → Spawn Timer`.

### Step 3 — Create a Spawn Table

1. `Create → Ueq → Spawn Table` in the Project window.
2. Add one or more entries. Set weights to control probability.
3. Assign a **Default Timer**.

### Step 4 — Place a Spawn Point

1. Create an empty GameObject at the desired world position.
2. Add the `SpawnPoint` component.
3. Assign the spawn table. Optionally override the timer.
4. Verify the spawn position is on the NavMesh.
5. Enter Play mode — the mob should appear once a player enters the activation radius.

---

## Common Recipes

### Single fixed mob (no randomness)

SpawnTable with one entry, weight 1. The weight value does not matter when there is only one entry — it always wins the roll.

### Placeholder + named

```
SpawnTable "Camp A":
  Orc Pawn       weight 19
  Gruul (named)  weight 1
```

Both entries share the same spawn point. The named appears ~5% of the time.

### Multiple mobs sharing a table

Create one `SpawnTable` asset. Assign it to each spawn point in the camp. When you adjust the table weights, all points update automatically.

### Fast-respawn test environment

Create a `SpawnTimer` with `baseSeconds = 5, variance = 0`. Assign it as a timer override on a single spawn point while testing. Swap it back to the standard timer before shipping.

### Different timers per point from the same table

Assign the same `SpawnTable` to all points, but set a `Timer Override` on specific points. The override takes precedence over the table's default timer for that point only.
