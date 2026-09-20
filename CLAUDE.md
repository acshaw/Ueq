# Ueq — Design Doc

## Project Overview
Unity URP multiplayer game using Mirror networking. Third-person-style with click-to-target combat. Currently in early prototype phase.

## Tech Stack
- **Engine**: Unity (URP)
- **Networking**: Mirror (KCP/UDP transport)
- **Input**: Unity Input System (polling, not callbacks)
- **Scene**: `Assets/Scenes/SampleScene.unity`
- **Editor tool**: `Tools/Setup Player Scene` bootstraps the scene from scratch
- **Content DB**: Postgres in Docker; content (items/abilities/races/classes/mobs/etc.) is authored via a web admin (Angular + ASP.NET Core API in `web/` + `api/`), not Unity ScriptableObjects
- **Root `README.md` is just a placeholder** — this section is the real setup doc

## Getting Started (local dev environment)
Three independently-runnable pieces: the Postgres DB, the Unity game (client+server in one), and the optional web content-admin app.

1. **Start Postgres** (Docker Desktop must be running):
   - Copy `.env.example` → `.env` if you haven't (already gitignored; defaults are fine for local dev)
   - `docker compose up -d` from the repo root
2. **Configure the DB connection**: `db.config.json` at the repo root (copy from `db.config.example.json` if missing) — must match `.env`'s user/password/db name. Also gitignored.
3. **Open the project in Unity** (Unity 6000.4.0f1) — let it finish compiling.
4. **Apply schema + seed content** (menu items added by `DatabaseTools.cs`):
   - `Tools/Database/Run Migrations` — applies any pending `.sql` files in `Assets/StreamingAssets/Database/Migrations/`
   - `Tools/Database/Seed Database` — loads starter content (items/mobs/etc.); **migrations alone do NOT seed data**, this is a separate step people forget
5. **Press Play** in the Unity Editor (or use `Tools/Setup All` first if the scene is missing core objects) — the same process runs as host (client+server). The server aborts loudly on start if it can't reach Postgres.
6. **Optional: run the web content-admin app** (only needed to author/edit game content, not to just play):
   - API: `cd api && dotnet run` (serves the content REST API used by the Angular app)
   - Web: `cd web && npm install && ng serve` → open `http://localhost:4200`

For multi-client testing without building a standalone player, use Unity's **Multiplayer Play Mode (MPPM)** package (`Window → Multiplayer Play Mode`) — standalone builds have had an unresolved corruption issue on this machine (see roadmap 5.10).

## Architecture

### Player
- `NetworkedPlayer` (Mirror `NetworkBehaviour`) — authoritative movement + look + targeting
  - RMB held = look mode (cursor locked); LMB click = target selection
  - `CmdSendInput` syncs move/yaw/sprint/jump to server each frame
  - `CmdAttack` is stubbed — no damage logic yet
- `PlayerController` — standalone (non-Mirror) fallback for local testing
- `Health` — generic health component; `SyncVar` + events for UI/VFX hooks; `EffectiveMax` uses class HP formula for players, mob definition for enemies, serialized fallback otherwise; `RefreshMax()` called on level-up or race/class change
- `PlayerAutoAttack` — key 1 toggles autoattack; derives ATK from STR/DEX weighted by `WeaponCategory` (Might/Finesse); hit roll shifted by (ATK-AGI)×shiftFactor; Glancing/Normal/Solid multipliers with variance; miss sends combat chat message; Critical fires as Solid until skill system exists
- `PlayerMana` — mana pool NetworkBehaviour; `SyncVar` current/max; `ComputeMax()` uses INT or WIS per class `ManaStatType`; `UseMana`/`RestoreMana`/`RefreshMax`; zero max for classes with `ManaStatType.None`
- `PlayerRegen` — server-side HP + mana regen; 1/tick every 6s per-player; sitting (2/tick) deferred; only ticks when alive and below max
- `PlayerAbilities` (NetworkBehaviour) — hotbar (8 slots, keys 2–9), known abilities, GCD + linked-timer cooldown engine; `SyncList<string>` hotbar + known; `SyncList<float>` per-slot cooldown for UI; `TryCast` validates range/mana/cooldown then calls effects in sequence; `SetRaceClass` populates known abilities from `ClassDefinition.startingAbilities`
- `AbilityDefinition` (ScriptableObject, `Assets/Ueq/Ability Definition`) — id, name, targetingType, range, castTime, manaCost, `List<AbilityTag>` tags (semantic), `List<CooldownLink>` cooldownLinks (empty = GCD), `List<AbilityEffect>` effects; assets in `Resources/Abilities/`
- `AbilityTag` (ScriptableObject, `Assets/Ueq/Ability Tag`) — pure semantic label (tagId, displayName); used in cooldown links and future systems (combos, elemental typing); assets in `Assets/ScriptableObjects/AbilityTags/`
- `CooldownLink` (serializable struct) — `{AbilityTag tag, float duration}`; when ability cast, starts shared timer for that tag; ability on cooldown if any linked tag timer > 0; empty links → GCD applies instead
- `AbilityEffect` (abstract ScriptableObject) — `Apply(caster, target, source)`; concrete: `DamageEffect` (baseDamage + stat scaling → `Health.TakeDamage`), `HealEffect` (baseHeal + stat scaling → `Health.Heal`); both support `ScalingStatType` enum
- `AbilityRegistry` (MonoBehaviour singleton) — loads `AbilityDefinition` assets from `Resources/Abilities/`; `Get(id)` lookup
- `HotbarUI` / `HotbarSlotUI` — 8-slot bar bottom-center; name label + cooldown countdown + grey overlay per slot; `Tools/Setup All` creates `HotbarCanvas`
- **Ability Editor** — `Tools/Editor/Ability Editor`; left panel lists assets; right panel: Identity, Targeting, Resource, Tags, Cooldown Links, Effects sections; creates in `Resources/Abilities/`; warns if not in Resources
- **Ability Tag Editor** — `Tools/Editor/Ability Tag Editor`; CRUD for `AbilityTag` assets in `Assets/ScriptableObjects/AbilityTags/`
- Race & Class Editor gains **Known Abilities** section on Classes tab (backed by `ClassDefinition.startingAbilities`)

### Enemy
- `Enemy` (`NetworkBehaviour`) — requires `Health` + `Targetable`; drives the AI state machine
- `EnemyAI` — server-only state machine (Idle → Chase → Combat → Return); uses NavMeshAgent
- `Targetable` — attach to anything selectable; `SetHighlight()` tints via `MaterialPropertyBlock`

### Combat
- `MobDefinition` (ScriptableObject) — all mob data in one asset: identity, combat, movement, AI, faction
- `MobApplicator` — component on Enemy prefab; holds `MobDefinition` ref; sets GameObject name + NavMeshAgent speed in `Awake`
- `Health`, `EnemyAI`, `NpcFaction`, `NpcEventDispatcher` each cache a `MobApplicator` ref in `Awake` and pull values from the definition (priority: MobDefinition → own serialized field)
- `NetworkedPlayer.ServerTarget` (NetworkIdentity) — set via `CmdSetTarget` on target change; server-side read point for autoattack
- `MobKillReward` (NetworkBehaviour, `IOnDeath`) — on mob death, awards `xpReward` XP directly to the killing player; Reward channel chat messages; fires after `Corpse.OnDeath`
- **Mob Editor** — `Tools/Mob Editor` EditorWindow; left panel lists all `MobDefinition` assets, right panel edits in sections; creates assets in `Assets/ScriptableObjects/Mobs/`
- **Race & Class Editor** — `Tools/Race & Class Editor` EditorWindow; three tabs: Races, Classes, XP Table; list + editor per tab; inline modifier summary ("20% more XP required"); XP Table tab: scrollable per-level grid (XP to complete + running cumulative), Reset to Defaults button; creates race/class assets in `Assets/ScriptableObjects/Races|Classes/`; XP Table asset lives at `Assets/Resources/XpTable.asset` (loaded via `Resources.Load`); "Create Default XP Table" button bootstraps it if missing
- `XpTableDefinition` (ScriptableObject) — holds `int[] xpPerLevel`; `PlayerExperience` loads it from Resources at runtime, falls back to `DefaultValues` if asset is absent

### Spawn System
- `SpawnTimer` (ScriptableObject) — `baseSeconds` + `variance`; `Roll()` returns randomized delay; shareable across spawn points
- `SpawnTable` (ScriptableObject) — weighted list of `SpawnTableEntry` (mob + weight + groupSize hook); `Roll()` picks an entry; holds a `defaultTimer`
- `SpawnPoint` (MonoBehaviour) — placed in scene; polls every 5s for players within `activationRadius`; spawns on activation, respawns after timer on mob death; inactive points pause respawn (mob spawns on next player arrival)
- `MobDefinition.prefab` — the base prefab to instantiate for this mob type
- `MobApplicator.SetDefinition()` — called by SpawnPoint after Instantiate, before NetworkServer.Spawn, so OnStartServer reads the correct definition
- `EnemyAI` resolves its values in `OnStartServer` (not Awake) to guarantee SetDefinition has already run

### Inventory
- `ItemDefinition` (ScriptableObject, `Assets/Ueq/Item Definition`) — `itemId`, `displayName`, `description`, `maxStackSize`; `IsStackable` computed property
- `InventorySlot` struct — `itemId` + `quantity`; Mirror NetworkWriter/NetworkReader extensions in `InventorySlotSerializer`
- `ItemRegistry` (MonoBehaviour singleton) — loads `ItemDefinition` assets from `Resources/Items/` at runtime; `Register(def)` for editor-time additions
- `PlayerInventory` (NetworkBehaviour) — `SyncList<InventorySlot>` (8 slots), SyncVar currency fields; `AddItem`/`RemoveItem`/`HasItem`/`IsFull`; `AddCurrency`/`SpendCurrency` (10:1 PP→GP→SP→CP normalization)
- `InventoryUI` — B key toggle (suppressed when chat open); late-binds to local player's `PlayerInventory` on first Update; refreshes slot labels + currency line via `SyncList.Callback`

### Network
- `GameNetworkManager` extends `NetworkManager`; logs connect/disconnect; spawn points via `NetworkStartPosition`
- Player prefab: `Assets/Prefabs/Player.prefab`

### Zones (M3.0 / 3.0.2)
- **Model:** single-server, multi-scene. Each zone is an additively-loaded scene authored at a distinct **world-space offset** (creslins_field=0 [base scene], thornwood=+5000, grukmars_deep=+10000); the base scene stays the active `SampleScene`. `ZoneInterestManagement : SceneInterestManagement` partitions observers by scene (players in different zones don't see each other/mobs). Player/mob `NetworkTransform` sync in **World** coordinate space.
- `ZoneCatalog` (ScriptableObject in `Resources/`) — boot list of `ZoneDefinition` (zoneId → sceneName → worldOffset → isBaseScene). Not DB-backed (a zone's scene is a Unity build).
- `ZoneManager` (on the NetworkManager GO, server-only) — loads the catalog, additively loads non-base zones, indexes each zone's entries + portals, polls portals for player proximity, and performs transitions. `ServerMovePlayer(conn, zoneId, entryId)` = `MoveGameObjectToScene` → client `SceneMessage` (additive load/unload) → `NetworkTransform.ServerTeleport` → chat-grid resync; supports **intra-zone** teleports (same-zone target warps without a scene swap). `ServerPlaceInZone(...)` does login/respawn placement into a persisted zone.
- `ZonePortal` (in-scene marker) — proximity portal (server poll, not a physics trigger — spike finding); `targetZoneId` + `targetEntryId` + `radius`; spawns a cosmetic cyan pillar at runtime. `ZoneEntry` — named arrival point (`entryId`); its transform position **and yaw** set the arrival spot + facing (NWN-style waypoint). Both are drag-in prefabs (`Assets/Prefabs/Zones/`) with Scene-view gizmo labels.
- **Zone-aware systems:** `characters.zone_id` persists (migration 0017) → login spawns into the saved zone; `SpawnPoint` moves spawned mobs into its own zone scene; `PlayerCorpse` + death-respawn are zone-aware; `ChatManager` delivers via `conn.Send` (survives interest partitioning). Baked NavMesh per zone is **persisted as an asset** so it reloads with the additive scene.
- **Authoring:** `Tools/Zones/Build Zone Scenes` (builds the 3 MVP zones + linear portal graph), `Tools/Zones/New Zone…` (stamp a new flat zone), `Tools/Zones/Create Zone Prefabs`. Generalized `ZoneSetup.BuildFlatZone(...)`. Zone **content/decoration is 3.1** (current zones are flat scaffolds ~280u each).

## Systems Design

### Faction System

**Model:** Named thresholds on a numeric scale, EQ-style. Thresholds defined in data (ScriptableObject) so new standings can be added without code changes.

Default thresholds (low → high standing):
`KOS → Threatening → Dubious → Apprehensive → Indifferent → Amiable → Kindly → Warmly → Ally`

**Player faction scores:**
- Every player has a numeric score for every faction in the game
- Scores initialize at character creation from a **race defaults table** (e.g., Troll + Qeynos Guards → -10,000; Dwarf + Qeynos Guards → -1,000)
- Scores are **per-player** — two trolls can diverge through individual choices over time
- Default for any faction not covered by the race table: **Indifferent (0)**

**NPC faction membership:** each NPC belongs to **one faction**

**Evaluation:** when an NPC perceives a player → look up player's score for this NPC's faction → map to threshold → drive behavior

**Faction hit sources:** kills, quests, items, dialogue choices, spells, abilities

**NPC-to-NPC interaction:** faction-level ally/neutral/hostile lists (not the numeric score system) — used for patrol behavior, guards responding to nearby fights, etc.

**Disguise / Illusion:**
- Players can have an apparent race/form separate from their actual one (bard masks, enchanter illusions)
- Faction checks use apparent identity — the NPC evaluates based on what it perceives
- Illusion is a clean identity substitution: the check uses the illusioned race's default score for that faction, ignoring the player's earned score entirely. The masked player is treated as an unrecognizable person who happens to measure as that race.

**Perception check (OnPerceived):**
1. Determine player's apparent race (accounting for active illusions)
2. Look up player's score for this NPC's faction
3. Map score to named threshold
4. Drive behavior: KOS/Threatening → aggro; Dubious/Apprehensive → warning; Indifferent+ → ignore or greet

---

### Conversation System

**Model:** Keyword listener — the NPC monitors nearby chat for trigger words. No dialogue trees, no clickable UI. Entirely text/chat driven. NPC responses are open chat (visible to all players in range).

**Keyword modes:**
- **Passive** — NPC always listening within a configured range tier, no hail required, no conversation lock. Used for passwords, magical wards, ambient triggers, custom conversation openers. Uses its own range/LOS check — independent of the targeting system (e.g., NPC behind a door can hear a password without being targetable).
- **Active** — only fires within an established conversation session (post-hail, locked to that player).

**Conversation state machine:**
```
Idle → InConversation(playerId) → Idle
              ↓ timeout
         OnConversationEnd (canned closing line) + player cooldown → Idle
```
- `Idle`: NPC hears passive keywords from any player in range
- `InConversation`: locked to one player; other players' active keywords ignored; indicator visible to nearby players
- `OnConversationEnd`: fires on timeout, closing keyword ("farewell"), or player leaving short range

**Hail:** a reserved passive keyword that triggers `OnConversationStart`. Extensible — any passive keyword can be designated as a conversation opener (e.g., a knock, a secret phrase, a custom greeting per NPC).

**Keyword matching flow:**
1. Player types in chat within NPC's configured range tier
2. NPC checks keyword mode: passive (always evaluates) or active (only if in conversation with this player)
3. If match: check faction requirement for that response
4. If faction requirement met: NPC delivers response, optionally applies faction hits, fires script events, unlocks additional active keywords for this player
5. Keyword progression is per-player and session-scoped

**Faction gating:** per-response requirement, not a gate on access. Player can always type — NPC just won't respond if standing is insufficient.

**Response markup:** response strings support substitution tokens resolved at delivery time from the triggering player's character data. Minimum token set: `<name>`, `<race>`, `<class>`, `<gender>`. Example: `"Well met, <class>!"` → `"Well met, Ranger!"`. Authoring happens in the conversation editor (future tooling); the runtime just performs token substitution before delivering the response.

**Quest model:** no explicit quest UI or quest-giving. Players infer objectives from dialogue and environmental clues.

**NPC-initiated speech:** NPCs can speak unprompted via:
- `OnPerceived` — greeting or hostile warning based on faction standing and range tier
- Time-based triggers (`IOnTimer`) — ambient dialogue on a configurable interval
- Any script event — any NPC event can trigger a say/emote response

**Keyword persistence:** unlocked keywords are session-scoped only — reset when the conversation ends. No server-side tracking of per-player keyword history.

---

### Enemy AI System

**Movement:** Unity NavMesh (NavMeshAgent) — required for obstacle avoidance. Scene must have navmesh baked.

**State machine:**
```
Idle/Wander → Perceive → Chase → Combat → (target dead) → Return → Idle/Wander
```

| State | Behavior |
|---|---|
| `Idle` | Random wander within a wander radius around spawn point |
| `Chase` | NavMesh path to target; re-evaluates each tick |
| `Combat` | In attack range — stop moving, auto-attack |
| `Return` | NavMesh path back to spawn point; heals/resets on arrival |

**Aggro entry — faction-driven (current):**
- `IOnPerceived` fires when a player enters perception range
- Resolve player's faction standing with this NPC's faction
- `KOS` or `Threatening` → enter Chase state
- `Dubious`/`Apprehensive` → warn only (no aggro)
- `Indifferent`+ → ignore

**Aggro entry — social (future):**
- When an NPC enters Combat, it broadcasts to nearby NPCs of the same faction
- Allies within social radius aggro the same target without a separate perception check

**Target reassessment (after kill):**
1. Enemy's current target dies → scan for nearest player within aggro range
2. If found → re-enter Chase with new target
3. If none → enter Return state, then resume Idle/Wander

**Wander (idle behavior — current):** pick a random NavMesh point within `wanderRadius` of spawn, walk there, pause, repeat. Wander replaced by waypoint patrol when `NpcWaypoints` component is present (future).

**Waypoint patrol (future):** `NpcWaypoints` component holds an ordered list of world-space transforms. Enemy walks the loop; wander is disabled while waypoints are active. Pause duration configurable per point.

**Key decisions:**
- NavMesh over CharacterController steering — obstacles in scene make raw steering unreliable
- State machine lives on server only (`[Server]` methods); position synced via `NetworkTransformReliable`
- Aggro type (faction vs. social) is a component flag, not a subclass — same `EnemyAI`, different config

---

### NPC Script Event System

**Model:** Interface-based components (Option A). Each behavior is a MonoBehaviour implementing one or more event interfaces. The NPC discovers them via `GetComponents` at runtime.

**Event interfaces (planned):**

| Event | Authority | Trigger |
|---|---|---|
| `IOnSpawned` | Server | NPC enters world |
| `IOnPerceived` | Server | Player enters perception range |
| `IOnTargeted` | Client | Player clicks to target this NPC |
| `IOnConversationKeyword` | Server | Matching keyword heard in chat |
| `IOnAttacked` | Server | NPC receives damage |
| `IOnFactionChanged` | Server | NPC's standing with a player changes |
| `IOnAggroLost` | Server | NPC loses its target (all threats cleared) |
| `IOnDeath` | Server | NPC health reaches zero |
| `IOnConversationStart` | Server | Player hails (or passive opener keyword matched) |
| `IOnConversationEnd` | Server | Conversation ends naturally, times out, or player leaves range |
| `IOnTimer` | Server | Periodic tick for ambient behavior |

**OnPerceived** is the faction system entry point — it resolves standing and routes to aggro, warning, or greeting behavior.

---

## Current Status
| Area | State |
|---|---|
| Player movement (walk/sprint/jump) | Working |
| Mouse look (RMB) | Working |
| Click-to-target + highlight | Working |
| Health system (SyncVar) | Working |
| NPC Event System | Working |
| Faction System | Working |
| Conversation System | Working |
| Enemy AI / movement | Working |
| Player autoattack (key 1 toggle) | Working |
| Enemy autoattack | Working |
| Aggro system | Placeholder only — needs design |
| Floating nameplates (mobs + players) | Working |
| Chat system | Working |
| Zones (3-zone MVP: transitions, interest isolation, per-zone spawn, persistence) | Working — flat scaffolds, content in 3.1 |
| Inventory (8 slots + PP/GP/SP/CP) | Working |
| UI (health bar, target frame) | Working |
| UI (inventory window) | Basic — needs polish |
| UI panel lock/unlock (context menu) | Not started |
| Win/lose conditions | Not started |

## Roadmap

> **Forward planning now lives in [`roadmap.md`](roadmap.md)** (numbered milestones/items, devplan-driven). Current milestone: **M1 — Identity & Persistence** (Postgres-in-Docker). **Workflow rule: every roadmap item gets a reviewed devplan in `docs/devplans/<id>-<slug>.md` BEFORE implementation.** The phased list below is retained as the history of what's been built; treat `roadmap.md` as authoritative for what's next.

### Phase 1 — Close the Core Loop
> Goal: fight, die, and progress. Everything else builds on this.

- [x] **Death handling** — player drops corpse with all items + copper; 10% XP loss (of next level's full cost); respawn at bind point (hardcoded to spawn position; `SetBindPoint()` wired for future spells/abilities); corpse owner-only lootable; full corpse looting recovers half the lost XP; de-leveling on XP loss supported
- [x] **XP + money on kill** — `MobKillReward` component implements `IOnDeath`; reads `xpReward` from `MobDefinition`; awards directly to attacker; Reward channel chat messages; level-up notification on XP award
- [x] **Level system** — 50-level XP table (1,000 → 10,291,400 per level); `PlayerExperience` uses static `XpTable[]` array; `ComputeLevel` / `TotalXpToReachLevel` / `XpForLevel` updated; `MaxLevel = 50`; level-up + de-level chat notifications; death XP loss = 10% of current level's span. `MobDefinition` gains `mobLevel` field (identity, shown in Mob Editor). Race + class XP modifiers: `RaceDefinition` and `ClassDefinition` ScriptableObjects (`Assets/Ueq/Race Definition`, `Assets/Ueq/Class Definition`) each carry a float `xpModifier`; `PlayerExperience` syncs combined `_xpModifier` SyncVar (race × class); all threshold math applies it; `SetRaceClass()` called at character creation (Phase 4). Inspector fields `_defaultRace`/`_defaultClass` on player prefab for pre-Phase-4 testing. Note: `RaceDefaultsTable` still uses string race names — migrate to `RaceDefinition` refs in Phase 4.

### Phase 2 — Make Characters Matter
> Goal: individual identity and meaningful gear choices.

- [x] **Character stats** — per-player STR/STA/AGI/DEX/INT/WIS/CHA; `CharacterStats` NetworkBehaviour (base from class, racial modifiers, equipment bonuses, SyncVar totals); `CombatStats` retired; Race & Class Editor exposes stat fields; `PlayerExperience.SetRaceClass` forwards to `CharacterStats.SetRaceClass`
- [x] **Hitpoint calculation** — `effectiveSta = min(Sta, classStaCap)` → `staModifier = classBaseStaRatio + (level-1) × classStaGrowthRate` → `maxHP = classBaseHP + (level-1) × classHpPerLevel + effectiveSta × staModifier`. Five new fields on `ClassDefinition`: `classBaseHP` (15/14/13/12), `hpPerLevel` (4/3/2/1), `staCap` (255/180/140/100), `baseStaRatio` (0.23 all), `staGrowthRate` (0.15/0.13/0.12/0.12) for melee/hybrid/healer/caster. Starting STA expected 55-105; level 1 HP lands 30-39 across archetypes; level 50 geared-to-cap: melee 2143, hybrid 1349, healer 966, caster 672. `Health.EffectiveMax` computes dynamically; `RefreshMax()` called on level-up and race/class change; falls back to flat `maxHealth` when no class assigned. **Pending editor setup**: configure ClassDefinition HP fields, assign `_defaultClass` on `PlayerExperience`
- [x] **Mana calculation** — `effectiveStat = min(INT or WIS, classManaCap)` → `manaModifier = classManaBaseRatio + (level-1) × classManaGrowthRate` → `maxMana = classBaseMana + (level-1) × classManaPerLevel + effectiveStat × manaModifier`. Mana stat per-class: INT → Wizard/Mage/Necro/Enchanter/SK/Bard; WIS → Cleric/Druid/Shaman/Paladin/Ranger; none → Warrior/Monk/Rogue (`ManaStatType.None` → max = 0). Five fields per class: `baseMana` (40/35/25), `manaPerLevel` (5/4/3), `manaCap` (200/200/150), `baseManaRatio` (0.23 all), `manaGrowthRate` (0.18/0.16/0.11) for caster/healer/hybrid. Level 1: 43-58 mana. Level 50 geared-to-cap: caster 2095, healer 1845, hybrid 1015. `PlayerMana` NetworkBehaviour with `UseMana`/`RestoreMana`/`RefreshMax`. **Pending editor setup**: configure ClassDefinition mana fields
- [x] **Combat calculation** — Weapons carry `baseDamage`, `delay`, and category (`Might` or `Finesse`). `ATK` is a derived stat: Might → `(STR×0.75)+(DEX×0.25)`; Finesse → `(DEX×0.75)+(STR×0.25)`. Per swing: (1) Dodge/Parry/Block — deferred (no skill system yet); (2) `hitRoll = clamp(rand(0,100) + (ATK-AGI) × shiftFactor, 0, 100)` maps to Miss (0-2.5) / Glancing (2.5-25) / Normal (25-75) / Solid (75-97.5) / Critical (97.5-100); (3) `rawDamage = weaponBase × (1 + relevantStat/400) × typeMultiplier × variance` — multipliers: Miss 0%, Glancing 50%±10%, Normal 100%±15%, Solid 150%±15%, Critical → Solid (skill untrained); (4) `finalDamage = rawDamage × (1 - AC/1000)` — AC=0 until equipment exists. `shiftFactor = 0.15`. Miss sends combat chat message instead of calling TakeDamage. Serialized weapon fields on `PlayerAutoAttack` stand in for equipment
- [ ] **Aggro / threat calculation** — numeric threat table per mob; threat generated by damage, heals, and abilities; formula deferred to grouping phase (threat only matters in group context). Current state: `EnemyAI` has threat list API (`AddThreat`) wired to damage, but formula is 1:1 damage = threat with no decay
- [x] **Hitpoint recovery** — `PlayerRegen` NetworkBehaviour; 1 HP/tick, 6s per-player server-side tick; only when alive and below max. Sitting (2/tick) designed, deferred until sitting state exists
- [x] **Mana recovery** — same `PlayerRegen` tick as HP; 1 mana/tick, 6s interval. Sitting (2/tick) deferred
- [x] **Equipment system** — equip slots (head, chest, legs, hands, feet, back, neck, 2× ring, 2× ear, weapon, offhand); item stats modify character stats via `CharacterStats.AddEquipmentBonus` / `RemoveEquipmentBonus`; equip/unequip from inventory
- [ ] **Item values** — `buyPrice`/`sellPrice` on `ItemDefinition`; weight (future encumbrance hook)
- [x] **Ability system** — data-driven, effect-composition model:
  - `AbilityDefinition` (ScriptableObject) — id, name, targeting type, range, cast time, cooldown, resource cost, ordered `AbilityEffect` list
  - `AbilityEffect` (abstract ScriptableObject) — one concrete subclass per mechanic: `DamageEffect`, `HealEffect`, `DotEffect`, `HotEffect`, `BuffEffect`, `DebuffEffect`, `SnareEffect`, `RootEffect`, `StunEffect`, `FactionHitEffect`; server calls `Apply()` on each in sequence
  - `PlayerAbilities` (NetworkBehaviour) — known ability list, hotbar slots, cooldown tracking, resource (mana/endurance/focus per class)
  - `AbilityRegistry` — loads `AbilityDefinition` assets from `Resources/Abilities/` at runtime
  - `HotbarUI` — key-mapped slots (distinct from autoattack); cast bar for timed casts
  - New ability = new ScriptableObject asset; new mechanic = new `AbilityEffect` subclass; no existing code touched
  - Warrior abilities are free (no mana cost); balance comes from a complex cooldown configuration that forces meaningful choices about which skill is available at any given time
- [ ] **Player grouping** — form/disband party (up to 6); group member health frames in UI; shared XP split evenly among members in kill range; friendly-fire rules (group members cannot damage each other by default); `MobKillReward` updated to distribute XP across group; `/invite`, `/disband`, `/leave` chat commands

### Phase 3 — Economy and Content Hooks
> Goal: give players things to work toward and spend currency on.

- [x] **Shop (buy/sell)** — `VendorApplicator` + `VendorInventory` + `VendorUI`; buy/sell tabs; wired into conversation system via `IOnConversationKeyword` ("wares" keyword); `CmdBuyItem`/`CmdSellItem` on `NetworkedPlayer`; `ItemDefinition` gains `buyPrice`/`sellPrice`
- [ ] **Quest reward trigger** — `IOnConversationKeyword` action that grants XP + items + faction hits; no quest UI — reward fires on correct keyword with faction gate
- [ ] **NPC item giving** — player trades item to NPC via conversation keyword; server validates inventory, removes item, fires script event

### Phase 4 — World and Identity
> Goal: the player is someone, and there is somewhere to go.

- [ ] **Player creation** — name, race, class selection screen; race seeds faction defaults; class sets base stats and grants starting `AbilityDefinition` list
- [ ] **Aggro system** — replace placeholder; faction-driven perception (already designed); social aggro (same-faction NPCs join combat within radius)
- [ ] **Second zone** — zone transition architecture (trigger volume → loading screen → new scene); proof of concept with one additional area; determines what state crosses zone boundaries

### Phase 5 — Persistence
> Goal: progress survives a server restart. Build last — schema should be stable by now.

- [ ] **Database persistence** — server-side storage for: XP, level, character stats, faction scores, inventory, currency, bind point; connection pooling; session auth token

### Phase 6a — Content Scale
> Goal: enough content to fill the progression curve. Runs in parallel with Phase 4–5.

- [ ] Fill mob roster, zone layouts, and item pool to cover level range
- [ ] NPC waypoint patrol — `NpcWaypoints` component; replaces wander when present
- [ ] Conversation editor — EditorWindow for authoring `ConversationKeywordSet` assets
- [ ] NPC editor — EditorWindow for NPC config (faction, keywords, waypoints, vendor stock)
- [ ] Balance pass — XP curve, drop rates, spawn density, shop prices against real play data

### Phase 6b — Infrastructure Scale
> Goal: real players on real hardware. Unknowable until load data exists — don't over-engineer early.

- [ ] Load testing — find actual player ceiling per zone server
- [ ] Zone server architecture — one process per zone or shared; inter-zone communication
- [ ] Authentication + accounts — login, session tokens, transport-layer anti-cheat
- [ ] Persistence at scale — write throughput, cache strategy, connection pooling under load

### Phase 6c — Client Distribution
> Goal: players can connect without Unity installed.

- [ ] Build pipeline — automated client builds per platform
- [ ] Launcher / patcher — version check on launch, delta patching
- [ ] Update delivery — push client updates without full re-download

---

## Deferred / Backlog
Items that are designed but not yet scheduled into a phase:

- **Inventory UI polish** — now scheduled as roadmap 5.9.1 (icons) + 5.9.2 (description window, replaces
  the old "tooltip on hover" idea); borders remain unscheduled cosmetic polish.
- **Right-click context menu** — `UIPanelContextMenu` on all moveable panels; Lock/Unlock position+size
- **Chat extensions** — `/say`, `/shout` (wider range), `/whisper <name>` already implemented; `/group`, `/yell` future
- **Item pickup** — no pickup mechanic yet; loot window covers corpse looting
- `GameNetworkManager.SpawnEnemy` / `DespawnEnemy` server hooks

## Key Decisions
- **Cursor free by default** — RMB to look so LMB stays available for targeting
- **CharacterController** over Rigidbody for predictable networked movement
- `NetworkTransformReliable`: **players sync position + rotation (yaw)** — `NetworkedPlayer.Awake` forces `nt.syncRotation = true` so remote players visibly turn (the body transform is yaw-only; camera pitch is separate + local-only, and the local player's NT is disabled on clients so there's no camera-lag cost). Mobs still sync position only (rotation sync for mobs is a 3.0.1 open item). *(Was "position only, rotation client-side" — that only ever suited the local player and left remote players frozen-facing; fixed in 3.1 once multi-client testing surfaced it.)*
- **Damage sources** are all unified through `Health.TakeDamage` (abilities, spells, environment, auto-attack)
- **NPC events** are interface-based (`IOnSpawned`, `IOnPerceived`, etc.) discovered via `GetComponents` on `NpcEventDispatcher`
- **Zone architecture: single-server, multi-scene** (proven by spike 2.0, ✅ GO). A zone = an additively-loaded scene with `SceneInterestManagement` partitioning by scene + per-scene physics. Server-per-zone (EQ-style world+zone-server split) is the same abstraction at a larger deployment size, so it's **not a fork** — it's **deferred to M6b** and gated on load-test data, not intuition. To keep the future split cheap, treat **zone as first-class**: `zoneId` on entities + persisted character state, chat behind a per-zone delivery interface, persistence keyed so a zone could move. Transition rules (from the spike): server-authoritative movement, teleport via `NetworkTransform.ServerTeleport` (never a raw transform set), no client-side physics body on players, portals via server-side proximity (not physics triggers). Full detail in `docs/devplans/2.0-zone-architecture-spike.md`.

## Last Session
<!-- Updated automatically at end of each session -->
- 2026-09-19: **Wired the newly-imported `Assets/SimpleForestAnimal/` pack (Bear/Boar/Fox/Rabbit/Raccoon/Skunk/Wolf/Doe/Stag/Moose, 3 variants each, 29 prefabs total) into the existing `MobModelCatalog` pipeline — code only, not yet run in-editor.** Surveyed the mob-model system first (`MobModelCatalog.cs`/`MobModelRegistry.cs`/`MobModel.cs`/`Tools/Character/Build Mob Model Catalog`): a mob's body resolves client-side by `modelId`, matched by convention against the mob's own DB `mob_id` string (confirmed via `mob-editor.ts` — there's no `model_id` field in the web app, so pairing is "make the mob's `mob_id` equal the catalog entry's `modelId` exactly"); the shared `Enemy.prefab` needs no per-species prefab, only a catalog entry. Found the catalog-build tool only scans `Assets/Synty/**/Prefabs/Characters/`, so it wouldn't have picked up this pack. **Real bug caught before it would've shipped**: read the pack's `.controller` assets directly (all 5: Bear/Boar/Deer/Fox/Rabbit) and found they drive their blend tree off a float parameter named `Speed_f`, not `Speed` — the shared `PlayerAnimator` that auto-attaches to every mob body hardcodes `"Speed"`, so these animals would have spawned and stood frozen (SetFloat on a nonexistent hash silently no-ops). Fixed by adding an optional per-catalog-entry `speedParam` override: `MobModelCatalog.Entry.speedParam` → threaded through `MobModel.Build()` → `CharacterModelFactory.BuildFromPrefab(..., speedParam)` → new `PlayerAnimator.SetSpeedParam(string)` (re-hashes after Awake); all additive/optional, zero behavior change for existing Synty (Humanoid) entries which never pass it. Extended `Tools/Character/Build Mob Model Catalog` (`MobModelCatalogSetup.cs`) to also scan `Assets/SimpleForestAnimal/Prefabs`, auto-resolving each prefab's shared rig controller via a name-prefix map (`ForestAnimalRigs`: Wolf/Raccoon/Skunk→Fox rig, Doe/Stag/Moose→Deer rig, Bear/Boar/Rabbit own rigs) and setting `speedParam = "Speed_f"` automatically — so running the tool once needs no manual per-entry controller/param editing, only optional `modelId` renames and offset/pivot tuning. **Known gap, not fixed**: these controllers only have Locomotion + Eat states (no Attack/Death triggers), so `EnemyAI`'s swing-animation RPC and `Corpse`'s death trigger will silently no-op on these bodies — cosmetic only, combat math/death still function correctly, just no attack/death anim clip. **NOT run in-editor** — no Unity instance this session; reasoned from reading the actual `.controller` YAML and existing call sites, not assumed. **NEXT (user, in editor): recompile → `Tools/Character/Build Mob Model Catalog` → in the Inspector, rename entries' `modelId`s to whatever you'll type as each mob's `mob_id` in the web app (or leave as the raw prefab name, e.g. `Wolf_01`) → check offset/eulerOffset visually (pack's pivot/facing not verified) → in the web Mob Editor, create a mob whose `mob_id` exactly matches a catalog `modelId` → confirm the body appears and animates walk/idle correctly.** Also flagged, not acted on: the project already has two older, separate one-off models (`Assets/Wolf/Wolf_URP.prefab`, `Assets/Blink/.../Bear_4.prefab`) with working Attack/Death Animator triggers from an earlier session, never registered in the catalog — user's call whether to also register those (for combat-anim fidelity) alongside the new pack's Wolf/Bear, or standardize on Simple Forest Animal's set for now. **User decided: standardize on the new pack's set — the older `Assets/Wolf/`/`Assets/Blink/` Bear models stay unregistered/unused for now.** All uncommitted — user commits.

**(cont. — built the web Mob Editor's Body Model dropdown, closing out 3.1.10's "Increment C — optional next" gap, after running the catalog tool surfaced 352 total entries (only 29 of them the new forest animals — the other ~320 were every previously-unregistered Synty character across every pack, since the catalog-build tool's existing Synty-wide scan is a pre-existing feature, not something added this session) and free-typing an exact modelId string stopped being practical.)** First oriented on the roadmap after the user's ~month-long pause: confirmed 3.1.10 (the underlying catalog system) was fully ✅ done, the dropdown was only ever flagged as deferred/optional within it — no blocking prerequisite. Also surfaced 5.12 (sky/day-night cycle) as the actual last-open milestone item, with two specific unverified checks still pending (DC7's per-zone opt-out flag via a temporary Thornwood test, and a two-MPPM cross-zone simultaneity check) — user chose to do the dropdown first. **Implemented full-stack, both stacks build-verified clean (`dotnet build`, `ng build --configuration production`)**, following the exact design already sketched in the 3.1.10 devplan's "Increment C" note. Migration `0037_mob_model_lookup.sql` — `mobs.model_id` (nullable, deliberately **not** an FK into the new lookup table, so a mob referencing a since-removed catalog id keeps working via the mob_id convention fallback rather than breaking) + new `mob_models` lookup table. Unity: `MobSnapshot.ModelId` (appended as the SELECT list's last column specifically to avoid renumbering `MobRepository`'s existing positional reader indices) → `MobRegistry.Build` sets `def.modelId` (the field already existed on `MobDefinition`, just never wired from DB). New `Tools/Character/Sync Mob Model Catalog to Database` (`MobModelSyncTool.cs`) — reads `MobModelCatalog.asset`, full delete-then-reinsert into `mob_models` (no confirm gate needed, unlike the World Placement sync tools' deletion candidates, since this table is a pure derived cache with no user-authored content to lose). API: `Mob.ModelId` + new read-only `MobModel` entity/`MobModelsController` (`GET` only — same "nothing here has a meaningful web-authored value" reasoning as `WorldPlacementsController`). Angular: `mob.service.ts` gains `modelId` + a grid column; new `mob-model.service.ts`; `mob-editor.ts` gets a "Body model" dropdown (blank = convention fallback) that renders the mob's current value as a flagged "(not in catalog — stale?)" option if it's since fallen out of sync, rather than silently dropping a working reference — mirrors the stale-value pattern already used for `SpawnPointEditor`'s Unity-side dropdowns. Updated the 3.1.10 devplan's Increment C note (now ✅ Done, with a full implementation log + verification checklist) and roadmap.md's 3.1.10 line to match. **NOT verified in-editor** — no Unity instance this session. **NEXT (user, in editor): recompile → `Tools/Database/Run Migrations` (0037) → `Tools/Character/Sync Mob Model Catalog to Database` (confirm it logs syncing ~352 entries) → in the web Mob Editor, open a mob, confirm the Body Model dropdown is populated → pick one, save, confirm it persists → optionally test the stale-value path (remove a catalog entry, re-sync, reopen a mob that referenced it). Separately still outstanding from earlier in this session: recompile → `Tools/Character/Build Mob Model Catalog` → rename the 29 forest-animal entries' `modelId`s if desired → check their offset/eulerOffset visually. And still open from before the pause: 5.12's two pending verification checks (DC7 zone-flag test, cross-zone simultaneity).**

**✅ Verified in-editor 2026-09-20 — Increment C confirmed working, one setup-order snag along the way.** User hit `PostgresException: 42P01: relation "mob_models" does not exist` on the first sync attempt — had run `Tools/Character/Sync Mob Model Catalog to Database` before `Tools/Database/Run Migrations`, so migration 0037 hadn't been applied yet (not a code bug; confirmed by reading `MigrationRunner.cs` — file discovery/versioning was fine). Running migrations first fixed it. After that: the web Mob Editor's Body Model dropdown populated correctly and a mob saved with a chosen model id persisted fine. Devplan + roadmap updated to ✅ Verified. Stale-value-flagging path (catalog entry removed after a mob references it) not separately exercised — same code path as the working dropdown, low risk. All uncommitted — user commits.
- 2026-09-18: **Discussion only, no code changes.** User bought the full Synty asset library and asked how to install a Synty package — walked through the standard `.unitypackage` import flow (Assets → Import Package → Custom Package…, imports into `Assets/Synty/<PackName>/`, matching existing folders like `PolygonPrototype`/`PolygonAdventure`), flagged the project's known cross-pack material/UV mismatch lesson (don't reuse one pack's material on another pack's mesh — bit the project before with PolygonPrototype vs PolygonAdventure) and recommended selective import (skip Demo/Sample scenes) for large packs. User then asked whether to replace the current Mixamo-sourced humanoid animations with Synty's own animation packs now that they're owned. Gave a recommendation, not yet acted on: Synty animations should match the stylized low-poly look/proportions better and pair naturally with the already-imported POLYGON Fantasy Characters models, but the current animator/blend-tree setup was hard-won (see the 2026-06-17→06-19 T-pose/locomotion debugging saga) — recommended checking the purchased pack's Humanoid-avatar rig compatibility first, then staging the swap (idle/walk/run first) rather than ripping out the whole Mixamo set at once. **No decision made — user still weighing it. NEXT: user decides whether to swap; if yes, verify rig/retargeting compatibility before wiring in new clips.**

> **Older entries archived.** Everything from 2026-08-24 back through the project's 2026-04-28
> bootstrap lives in [`docs/session-log-archive.md`](docs/session-log-archive.md) (not
> auto-loaded — read it directly, including its own index, when you need historical detail).
> Archived in two passes: 2026-08-09 (through 2026-07-26) and 2026-09-19 (2026-07-27–2026-08-24,
> ~35KB removed). This note used to carry a full copy of the archive's index inline — that
> duplication was itself part of why this file kept regrowing, so it's gone; the index now lives
> only in the archive file. Going forward, periodically archive entries older than roughly the
> last 2-3 weeks the same way (move them into the archive file's own index + body, don't copy the
> index back here), and update this note's cutoff date.
