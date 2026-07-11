# Ueq Spawn System — User Guide

A practical, end-to-end guide to creating monsters and NPC spawns in Ueq. It's written so either of us can
follow it — no coding required for the day-to-day content work. Read the **Big Picture** first, then jump to the
**Recipes** for step-by-step examples.

---

## 1. The big picture

Making something appear in the world is **two separate jobs**:

1. **Define the creature** — *what* it is: its name, health, damage, faction, XP, loot, and which 3D body it
   wears. This is data, authored in the **web editors** (the Angular app in your browser) and stored in the
   database. Think of it as writing the creature's character sheet.

2. **Place a spawn** — *where and how* it appears: you drop a **Spawn Point** into the Unity scene and tell it
   which creature (or weighted list of creatures) to spawn there, how many, how often, and how it moves. This is
   done in the **Unity editor**.

> **Mental model:** the *Mob Definition* is the recipe; the *Spawn Point* is the oven that bakes copies of it at a
> spot in the world. One recipe can be used by many spawn points; one spawn point can roll from a weighted list of
> recipes.

### Where each piece lives

| Piece | What it is | Authored in | Stored in |
|---|---|---|---|
| **Mob Definition** | A creature's stats/identity | Web **Mob Editor** | Database |
| **Spawn Table** | A weighted list of mobs + group size + respawn timer | Web **Spawn Editor** | Database |
| **Mob body (model)** | The Synty 3D character a mob wears | Unity **Mob Model Catalog** | Unity project |
| **Spawn Point** | The in-world spawner | Unity **scene** | The scene file |
| **Patrol Route / Wander Region** | Optional movement shapes | Unity **scene** | The scene file |

### The three programs involved

To author and test content you need all three running:

1. **Postgres** (the database) — started with Docker: `docker compose up -d`.
2. **The web app** (Angular UI + the small API) — you start these from a terminal; the browser UI is where mobs,
   spawn tables, loot, factions, etc. are authored.
3. **Unity** — hosts the game server, which **loads all content from the database when the host starts**.

> ⚠️ **Important:** Unity reads the database **once, at host start.** If you edit a mob in the web app while the
> game is running, **restart the Unity host** to see the change.

---

## 2. Key concepts (glossary)

- **Mob id** — the unique internal name of a mob (e.g. `goblin_scout`). Spawn points and spawn tables reference
  mobs by this id. Distinct from the **display name** ("Goblin Scout") that players see.
- **Spawn Point** — a GameObject in the scene that spawns mobs when a player is near. Its yellow wire sphere is its
  **activation radius**.
- **Activation** — a spawn point checks every **5 seconds** whether any player is within its **activation radius**
  (default **50 units**). If yes and nothing is currently alive there, it spawns.
- **Respawn** — when everything a spawn point spawned has died, it waits for the **respawn timer**, then spawns
  again (only while a player is still nearby; otherwise it spawns fresh the next time a player arrives).
- **Movement mode** — how a spawned mob moves when it's *not* fighting: **leash** (default), **wander region**,
  **free-range**, or **patrol**. (Details in §5.)
- **Aggro** — whether a mob attacks a player. Driven by **faction standing** — a mob is hostile to players whose
  standing with its faction is low enough. A mob with no faction just stands there peacefully.
- **Mob body / model** — the visible Synty 3D character. Assigned separately from the mob's stats (see §6). A mob
  with no assigned body shows a placeholder white cube.

---

## 3. Two spawn styles: single mob vs. spawn table

A Spawn Point can spawn in one of two ways. It picks the first one that's filled in:

| Field on the Spawn Point | Behavior | Use it for |
|---|---|---|
| **`spawnTableId`** (highest priority) | Rolls a **weighted random** mob from a table, can spawn a **group**, uses the table's **respawn timer** | Wilderness camps, varied encounters, packs |
| **`mobId`** | Spawns **one specific** mob, respawns after a default 5-minute timer | Unique/named NPCs (a vendor, a boss, a quest-giver) |

If both are set, the **spawn table wins**. If neither is set, the spawn point warns and does nothing.

---

## 4. The lifecycle of a spawn (what happens at runtime)

1. **Idle** — no player nearby. Nothing spawned.
2. **Player enters the activation radius** (checked every 5s) → the spawn point spawns its mob(s).
   - Spawn table → rolls an entry, spawns `groupSize` copies (spread out a little so they don't stack).
   - Single mob → spawns one.
   - Each mob is dropped onto the ground/navmesh so it sits on hills correctly.
3. **Mobs live and behave** — wander/patrol, aggro nearby players by faction, chase, fight, and (on losing their
   target) return and heal.
4. **All spawned mobs die** → the spawn point starts the **respawn timer**.
   - Table timer = its base seconds ± variance. Single mob = 5 minutes.
   - If a player is still nearby when the timer elapses → respawn.
   - If no player is nearby → it will spawn fresh the next time a player arrives.

---

## 5. Movement modes

When a mob isn't fighting, its idle movement is one of four modes. A Spawn Point picks the mode by which field is
set — checked **in this order** (first match wins):

| Priority | Set this on the Spawn Point | Mode | What the mob does |
|---|---|---|---|
| 1 | **Patrol Route** | **Patrol** | Walks an ordered loop of waypoints; resumes its beat after a fight |
| 2 | **Wander Region** | **Bounded wander** | Wanders randomly **inside an authored box/sphere**, ignoring its spawn spot |
| 3 | **Free Range** (checkbox) | **Free-range wander** | Wanders the **whole zone** |
| 4 | *(none of the above)* | **Leash wander** (default) | Wanders within `wanderRadius` of its **spawn point** (from the Mob Definition, default 10u) |

Notes:
- A **Stationary** mob (its Mob Definition's `movementType = Stationary`) ignores regions/free-range — it just
  stands at its spawn.
- **Chasing is never limited.** A mob chases its target until the target dies or leaves the zone — movement modes
  only shape *idle* wandering, never the chase. (When a target zones out, the mob disengages and returns to its
  idle behavior.)
- **After a fight:** a *leashed* mob walks home to its spawn and heals; a *roaming* mob (region/free-range) resets
  where it stands. Patrollers walk back and rejoin the nearest waypoint.

---

## 6. Giving a mob a body (the 3D model)

A mob's **stats** and its **3D body** are separate. If you skip this step, the mob spawns as a plain white cube.

Bodies are managed in the **Mob Model Catalog**:

1. Menu: **Tools → Character → Build Mob Model Catalog**. This creates/updates
   `Assets/Resources/MobModelCatalog.asset` and auto-fills it with every imported Synty character prefab. Each
   entry has a **Model Id** (defaults to the prefab's name) and the **prefab**.
2. Open **`MobModelCatalog.asset`** (in `Assets/Resources/`).
3. Find an entry whose prefab is the body you want, and set its **Model Id** to your **mob id** (e.g. change an
   entry's Model Id to `goblin_scout`).
   - By default a mob looks for a body whose Model Id equals its **mob id**. You can also set an explicit
     `modelId` on the Mob Definition so several mobs share one body (e.g. three rat variants → one `giant_rat`
     body).
4. (Optional, non-Humanoid bodies) if a body isn't a Humanoid rig, set the entry's **animator controller** so it
   animates with its own clips. Leave it blank for normal Humanoid Synty characters (they reuse the shared
   locomotion).

> Shortcut convention: instead of the catalog, you can drop a prefab named exactly like the mob id into
> `Assets/Resources/MobModels/` and it'll be found automatically. The catalog is preferred because it references
> prefabs in place (no copying).

---

## 7. Recipes (step-by-step examples)

These assume Postgres + the web app are running, and you're in Unity. After any web-editor change, **restart the
Unity host** before testing.

### Recipe A — A single named NPC at a fixed spot

*Goal: one "Old Hermit" that always appears by the well.*

1. **Author the mob** (web Mob Editor):
   - New mob, id `old_hermit`, display name "Old Hermit".
   - Set health, and **leave the faction empty** so he's peaceful (no aggro).
   - Save.
2. **Give him a body** (§6): in the Mob Model Catalog, set a suitable entry's Model Id to `old_hermit`.
3. **Restart the Unity host** so it loads the new mob.
4. **Place the spawn** (Unity):
   - Aim the Scene view at the well → **Tools → Zones → Place Encounter (Spawn Point)**.
   - Select the new `Encounter (SpawnPoint)`; in the Inspector set **`mobId` = `old_hermit`** (leave
     `spawnTableId` empty).
   - Save the scene.
5. **Test:** Play as Host, walk near the well — the Old Hermit spawns and stands around (leash wander).

### Recipe B — A wilderness camp with a weighted mix (spawn table + groups)

*Goal: a clearing where mostly goblins spawn, occasionally a bigger brute, sometimes in pairs.*

1. **Author the mobs** (web Mob Editor): e.g. `goblin_scout` and `goblin_brute`, each with a hostile **faction**
   (see §8), XP reward, and a loot table. Give each a body (§6).
2. **Author a spawn table** (web Spawn Editor):
   - New table, id `greenwood_goblins`.
   - Entry 1: mob `goblin_scout`, **weight 4**, **group size 2**.
   - Entry 2: mob `goblin_brute`, **weight 1**, group size 1.
   - Set the **respawn timer**: base `45`, variance `15` (so ~30–60s).
   - Save. (Weights are relative — scouts here appear 4× as often as brutes.)
3. **Restart the Unity host.**
4. **Place the spawn** (Unity): Place Encounter → set **`spawnTableId` = `greenwood_goblins`** (leave `mobId`
   empty). Save the scene.
5. **Test:** walk into the clearing — a weighted roll spawns either a pair of scouts or a brute; when they're all
   dead it respawns after the timer.

> Place **several** spawn points that share the same `spawnTableId` to fill a larger area with the same encounter
> mix.

### Recipe C — A mob that roams a bounded area (Wander Region)

*Goal: a wolf that patrols a meadow, not tied to one bush.*

1. Author `dire_wolf` (hostile faction, body) and restart the host.
2. **Place the spawn** with `mobId = dire_wolf` (or a spawn table).
3. **Create the region:** aim the Scene view at the meadow → **Tools → Zones → New Wander Region**.
   - Select the `Wander Region`; in the Inspector choose **Box** or **Sphere** and size it to cover the meadow
     (the green gizmo shows the area). Make sure it sits over walkable ground (the navmesh).
4. **Link them:** select the Spawn Point → drag the `Wander Region` object into its **Wander Region** field.
5. Save the scene, restart, test: the wolf wanders anywhere inside the green volume, ignoring where it spawned.

### Recipe D — A free-range wanderer

*Goal: a lone ghost that drifts across the whole zone.*

1. Author `lost_ghost` + body, restart.
2. Place the spawn (`mobId = lost_ghost`).
3. On the Spawn Point, tick **Free Range** (leave Wander Region empty). Optionally raise **Free Range Radius** to
   widen its roaming (keep it comfortably under the distance between zones — the default is safe).
4. Save, restart, test: it roams the zone's walkable area.

### Recipe E — A patrolling guard

*Goal: a city guard that walks a fixed route down the road and back.*

1. Author `city_guard` (usually a **guard faction** that's friendly to players, so it won't attack you) + body,
   restart.
2. **Create the route:** aim the Scene view at the first post → **Tools → Zones → New Patrol Route**.
   - With the route selected, move the Scene camera to each next post and use **Tools → Zones → Add Patrol
     Waypoint** to drop points in order. The blue line + numbers show the path.
   - On the `Patrol Route`, set **Loop** (walk the loop) or uncheck it (ping-pong back and forth), and
     **Pause Per Point** (seconds paused at each post).
3. **Place the spawn** (`mobId = city_guard`) → drag the `Patrol Route` into its **Patrol Route** field.
4. Save, restart, test: the guard walks its beat, aggros anything hostile it perceives, and returns to the nearest
   post afterward.

---

## 8. Aggro in one paragraph

Whether a mob attacks you is decided by **faction standing**, not by the spawn system. In the web editors you give
a mob a **faction**; every player has a numeric standing with every faction (seeded from their race). When a mob
perceives a player within its **perception radius**, it looks up that standing: low enough → it attacks; middling
→ it warns; friendly → it ignores/greets you. So:
- **Peaceful NPC** (vendor, quest-giver): give it **no faction** (or a faction players stand well with).
- **Hostile monster:** give it a faction players stand poorly with (e.g. a "Monster" faction set hostile to all
  races). The example content's `Build Example Encounters` seeds exactly such a Monster faction as a reference.

(Full faction design lives in the design doc; richer threat/social aggro is a later milestone.)

---

## 9. A ready-made reference to copy

The fastest way to learn by example is the seeded starter content:

- **Tools → Database → Seed Database** then **Tools → Zones → Build Example Encounters (Creslins Field)** creates
  a Monster faction, three wilderness mobs (Goblin Scout / Skeleton Soldier / Goblin Warchief), a weighted spawn
  table, and three placed encounters near spawn (a random-table camp, a static boss, and a city-guard patrol
  loop). Open those in the Mob/Spawn editors and in the scene to see every field filled in correctly, then copy
  the pattern.

---

## 10. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| Mob spawns as a **white cube** | No body assigned. Do §6 (catalog Model Id = mob id). |
| **Nothing spawns** when I walk up | Not within the **activation radius** (default 50u); or neither `spawnTableId` nor `mobId` is set; or you're not running as **Host/Server** (spawns are server-side). Check the Console for a `[SpawnPoint]` warning. |
| My **web edit didn't show up** | Unity loads content at host start — **restart the Unity host**. Also confirm Postgres + the API are running. |
| Mob **won't wander into its region** / stands still | The Wander Region (or the spawn) isn't over the **navmesh**. Move it over walkable ground; confirm the green volume overlaps the blue navmesh. Also confirm the mob's `movementType` is **Wander**, not Stationary. |
| Mob **ignores my Wander Region** and stays near spawn | A **Patrol Route** is also set (patrol wins). Clear it. Or the mob is Stationary. |
| Mob **isn't hostile** | It has no faction, or the player's standing with its faction is high. Give it a hostile faction (see §8). |
| Mob **chases forever / off the map** | By design chasing has no distance leash; it stops when the target dies or **zones out**. If a target truly can't be reached, it'll keep trying until then. |
| Mob spawns **floating or buried** | `Snap To Ground` is off, or there's no ground collider / navmesh under the point. Keep Snap To Ground on and place over baked navmesh. |

---

## 11. Quick reference

### Spawn Point fields (Unity Inspector)

| Field | Meaning |
|---|---|
| `spawnTableId` | DB spawn table to roll from (weighted, grouped, timed). **Top priority.** |
| `mobId` | A single DB mob to spawn (used if no spawn table). |
| `activationRadius` | How close a player must be to trigger spawning (default 50). |
| `patrolRoute` | Optional patrol path (movement priority 1). |
| `wanderRegion` | Optional bounded roam area (movement priority 2). |
| `freeRange` / `freeRangeRadius` | Roam the whole zone (movement priority 3). |
| `snapToGround` / `groundMask` / `navSampleRadius` | Placement onto the ground + navmesh. |

### Mob Definition fields (web Mob Editor)

Identity (`displayName`, `mobLevel`, `modelId`) · Combat (`maxHealth`, `attackDamage`, `attackInterval`,
`attackRange`) · Movement (`movementType` Wander/Stationary, `moveSpeed`, `wanderRadius`, `wanderPauseMin/Max`) ·
AI (`perceptionRadius`, `baseAggroThreat`) · Faction (faction + aggro/warning standings + on-kill faction hits) ·
Loot (`lootTableId`) · Rewards (`xpReward`) · Vendor/Conversation ids (for shop/dialogue NPCs).

### Tools menu

| Menu | Does |
|---|---|
| **Tools → Zones → Place Encounter (Spawn Point)** | Drops a Spawn Point at the Scene-view focus |
| **Tools → Zones → New Wander Region** | Drops a box/sphere roam area |
| **Tools → Zones → New Patrol Route** / **Add Patrol Waypoint** | Builds a patrol path |
| **Tools → Character → Build Mob Model Catalog** | Registers Synty bodies; set an entry's Model Id to a mob id |
| **Tools → Database → Seed Database** | Loads the seeded reference content into the DB |
| **Tools → Zones → Build Example Encounters (Creslins Field)** | Places the reference camp/boss/patrol to copy |

---

*Questions or something behaving differently than described? The authoritative behavior lives in
`Assets/Scripts/Combat/SpawnPoint.cs`, `MobDefinition.cs`, `SpawnTable.cs`, and the movement behaviors under
`Assets/Scripts/NPC/`.*
