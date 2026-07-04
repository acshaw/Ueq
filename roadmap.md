# Ueq — Roadmap

Forward-looking, devplan-driven plan. This file is the **authoritative source for what's next**.
(`CLAUDE.md` keeps the per-session history of what's already been done.)

---

## How this roadmap works

### Numbering scheme

- **Milestones** — major themed phases: `M1`, `M2`, …
- **Roadmap items** — `<milestone>.<item>`: `1.1`, `1.2`, `1.3`, … These are the planned, reviewable units of work.
- **Inserted / iteration devplans** — work we discover mid-stream that belongs *between* two roadmap items gets a **third dotted level** under the item it follows. Example: a follow-up after `1.2` but before `1.3` becomes `1.2.1`, then `1.2.2`, …
  Dotted-decimal ordering guarantees `1.2 < 1.2.1 < 1.3`, so **inserting never forces a renumber**.

### Devplan-first workflow (required for every item)

1. Pick the next roadmap item.
2. **Write its devplan** at `docs/devplans/<id>-<kebab-slug>.md` — goal, approach, schema/API changes, files touched, test plan, risks.
3. **User reviews and approves** the devplan.
4. Implement.
5. Mark the item ✅ here with a link to its devplan.

No roadmap item gets implemented before its devplan is reviewed.

### Standing principle — "No content without a tool"

Every authored content type ships with an editor in the same change that introduces it, so content can be built without coding.

### Standing principle — "Retire the old field once the new one is proven"

When a content type is DB-migrated **and verified in-game**, remove its now-dead ScriptableObject authoring fields (and migrate any scene/prefab that referenced them) in the same pass — don't let deprecated fields accumulate, since two sources for one thing is what makes "which is authoritative?" confusing. **Exception:** fields that are *runtime-resolved* (e.g. `MobDefinition.faction`/`.lootTable`, which the registries populate from an id at load) are internal state, not authoring fields — keep them.

**Direction (2026-06-20):** content authoring moves to **web-based editors backed by Postgres**, and the game **server loads content from the DB at startup** — the DB is the source of truth, and shipping content no longer requires a Unity rebuild. Migration is incremental: ScriptableObjects keep working per type until that type's DB path + web editor lands, then its `Resources/` load is retired (hybrid-as-path, DB-at-startup as the destination).

Two kinds of data live in the DB, and they behave differently:
- **Content / definitions** (classes, races, abilities, skills, items, quests, loot tables, XP thresholds, mobs, vendors, factions, conversations) — authored occasionally in web editors, read at load.
- **Player / account runtime state** (XP, level, stats, faction scores, inventory, currency, equipment, bind, quest progress) — written every session (M1).

**Pure data vs. asset bindings:** stats, prices, text, drop weights, and formulas go in the DB. Unity **asset references** (prefabs, icons, meshes, animations) *cannot* — they're stored as string ids/paths and resolved at load via Resources/Addressables. This keeps the data web-authorable while asset wiring stays a small Unity concern.

---

## POC foundation (done)

Networked movement + camera, click-to-target combat, stat/HP/mana/regen formulas, auto-attack + ability system (data-driven, cooldowns), Synty character art + locomotion/attack/kick animations, terrained world with walkable hills + hill-aware mob spawns, enemy AI (wander/chase/combat/leash + threat list), factions, keyword conversations, chat, inventory + equipment + currency, vendors, loot + corpses, XP/levels, death/corpse-runs. Editor tools: Mob, Item, Ability, AbilityTag, Race & Class, Loot Table, Vendor.

Full history in `CLAUDE.md` (Current Status + Last Session).

---

## M1 — Identity & Persistence  *(current milestone)*

> Goal: characters survive a restart, behind accounts, created through a real first-run flow — then a client-structure cleanup (1.7) before content work begins.
> Storage: **PostgreSQL** in Docker locally (seeded as we go), migrating to AWS later (M5).
> Note: **1.1's DB + migration infrastructure is the shared foundation for both player state (this milestone) and DB-backed content (M2).**

- [x] **1.1 — Postgres + Docker dev environment.** ✅ Done 2026-06-20 ([devplan](docs/devplans/1.1-postgres-docker-env.md)). `docker-compose` Postgres 16, NuGetForUnity + Npgsql 6.0.11 (smoke-tested), `DatabaseConfig`/`Database`/`MigrationRunner`/`DatabaseSeeder`, `schema_version` + `0001_init.sql`, `Tools/Database/*` menus, `GameNetworkManager.OnStartServer` wiring with abort-on-DB-down. Verified in-editor (connect + idempotent migrate at host start). *(Shared foundation for M2 content too.)*
- [x] **1.2 — Server-side data-access layer.** ✅ Done 2026-06-20 ([devplan](docs/devplans/1.2-data-access-layer.md)). `PersistenceService` (worker thread + coalescing write queue + `LoadAsync` + main-thread pump + flush-on-stop), `ISaveJob`/repository conventions, `Database.RunInTransaction`, DAL self-test. Server-authoritative; DB writes off Mirror's tick. Verified in-editor (self-test PASS, clean flush on Stop Host).
- [x] **1.4 — Accounts + login.** ✅ Done 2026-06-21 ([devplan](docs/devplans/1.4-accounts-login.md)). Pulled ahead of 1.3 (review decision O1 — character persistence keys off a real account→character mapping). `accounts` table + `0004_accounts.sql` (PHC `pbkdf2$…` hashes, drops the 1.2 `dal_smoke` table), `PasswordHasher` (PBKDF2 via built-in `Rfc2898DeriveBytes`), `AccountRepository`, `AccountAuthenticator : NetworkAuthenticator` (async credential check off-thread via 1.2's `LoadAsync`, single-login map, account id stashed in `conn.authenticationData`), `LoginUI`, seeded `dev` account for one-click host. Verified via Multiplayer Play Mode (register / login / wrong-password / duplicate / already-online matrix all pass).
- [x] **1.3 — Character-state schema + save/load round-trip.** ✅ Done 2026-06-21 ([devplan](docs/devplans/1.3-character-state-persistence.md)). Persists the mutable set (XP, currency, inventory, equipment, faction scores, hotbar, current HP/mana, position/bind, race/class id) keyed off the account, and reconstructs everything derived on load. Normalized child tables (`characters` + `character_inventory`/`_equipment`/`_faction_scores`/`_hotbar`, FK to `accounts`, migration `0005`), `CharacterSnapshot`/`CharacterRepository`/`CharacterPersistence` on the 1.2 async queue, `RaceClassRegistry` (Race/Class assets moved to `Resources/`), manager-driven save-on-disconnect/stop. Verified in-editor (round-trip restores XP/items/equipment-bonus-once/hotbar/position; account isolation). Also fixed a pre-existing host-restart bug: `InventoryUI`/`EquipmentUI` now rebind to the new local player instead of latching `_bound`.
- [x] **1.5 — Character select + creation (first-run).** ✅ Done 2026-06-21 ([devplan](docs/devplans/1.5-character-select-creation.md)). `autoCreatePlayer=false` + a pre-spawn select/create handshake: `CharacterSelectController` (server) validates create/enter/delete and spawns via `AddPlayerForConnection`; `CharacterSelectUI` (IMGUI) shows the create form or the account's character. Create = enter (reuses 1.3's no-row init branch fed the chosen race/class/name; immediate save creates the row); name now drives nameplate + chat identity. Unique names enforced (`0006`, case-insensitive partial index). One character per account (D1); minimal confirmed delete (D7). `_defaultRace`/`_defaultClass` retired to a warned dev fallback (D5). Verified in editor + MPPM: create/enter/delete, duplicate/short-name rejection, one-char block, created-character persist round-trip, account isolation.
- [x] **1.6 — Save policy + multi-character.** ✅ Done 2026-06-21 ([devplan](docs/devplans/1.6-save-policy-multichar.md)). Persistence re-keyed from account → `character_id` (migration `0007` drops `UNIQUE(account_id)`; `Upsert`/`Load`/coalesce all key off character id) so an account holds multiple characters without cross-contamination; identity row created up front at creation (O2). 8-slot cap; 90s autosave tick + save-on-quit; "Camp to Character Select" (save + despawn + back to select, no disconnect); `≤0` HP fills to max on load; bind-point persistence confirmed. Verified: multi-character create/select/camp, A↔B independent across stop/restart. *(Camp/chat polish — countdown, not-in-combat gate, `/camp` + `/help`, chat-clear-on-switch, MOTD — split out to 1.6.1.)*
- [x] **1.6.1 — Camp & session polish.** ✅ Done 2026-06-22 ([devplan](docs/devplans/1.6.1-camp-session-polish.md)). `/camp` + `/help` (single command table); 10s cancelable camp countdown (`CampController`) gated by a new synced `CombatState` (stamped from `Health.TakeDamage`), server re-checks combat before despawn; pulsing red combat border on the HP frame; chat clears on character switch (`LocalPlayer.Despawned`) + server MOTD on entering. Verified in editor.
- [x] **1.7 — UI architecture refactor (client structure).** ✅ Done 2026-06-21 ([devplan](docs/devplans/1.7-ui-architecture-refactor.md)). Two passes: (A) `LocalPlayer` service (`Current` + `Spawned`/`Despawned`, fed by `NetworkedPlayer`) — all 8 panels rebound onto it, zero `FindObjectsByType<NetworkedPlayer>` left, retiring the per-panel bind-once fragility; (B) HUD canvases moved to an additive `UI.unity` (`Tools/Build UI Scene`) loaded by `UIManager` (skips headless), gameplay scene + `SetupScene` no longer build UI. **O5 deferred** — login/select/HUD kept IMGUI (polish, own later item). Verified in editor: HUD parity, camp/switch/host-restart rebinding all clean. *(Fixed a recurring hotbar slot-label wiring quirk via a direct `HotbarSlotUI.Init` — see below.)* Move the HUD/menus out of the gameplay scene into a dedicated **additive UI scene** loaded at startup, with real separation of concerns. The M1→M2 bridge: completes the *playable-client* foundation before content work. Scope:
  - **Additive UI scene + `UIManager`/`UIRoot` bootstrap** — load the UI scene additively; the UI layer persists across gameplay/zone scene loads (directly enables M3.5 zone transitions, where UI must not reload per zone).
  - **Centralized local-player resolution** — one place resolves "the local player" and raises a `localPlayerReady`/`changed` event; panels subscribe instead of each doing `FindObjectsByType<NetworkedPlayer>` / `NetworkClient.localPlayer`. Permanently retires the per-panel bind-once fragility (the host-restart bug patched in 1.3 was a symptom).
  - **View vs. controller split** per panel; **author UI as prefabs** instead of the ~900 lines of procedural `Create*UI()` in `SceneSetup.cs`.
  - Covers the ~10 current canvases (Chat, HUD, Inventory, Equipment, Vendor, Loot, Hotbar, Login, target/player frames) + the 1.5 character-select screen.
  - *Note (revised 2026-07-04):* the **full M1 regression is DEFERRED to pre-first-deployment**, not run per-milestone. 2.x (every content vertical migrated + re-tested) and 3.0 (which reworked spawn/chat/persistence/player-spawn/UI and was hammered across Stages A/B/C) have already re-covered most of what it checks, so a dedicated pass now is largely redundant. The **light M1 smoke** (boot → login → create → fight → camp → relog) is the interim bar; the full zone-aware checklist at `docs/m1-regression-checklist.md` is kept **current as systems change** and run as a gate before the first real deployment.

## Zone architecture spike  *(runs before 2.1 — de-risks the M3 zone model early)*

> Pulled forward 2026-06-22: the **concurrent multi-zone** requirement (P1 in Zone A while P2 in Zone B,
> on one server) breaks the current single-shared-scene model that M1 systems assume. Spike it cheaply
> **now**, before building content/systems on that assumption, so the M3 zone integration is informed
> and the starting zone (3.1) is built as a proper zone from day one.

- [x] **2.0 — Zone architecture spike (throwaway).** ([devplan](docs/devplans/2.0-zone-architecture-spike.md) — ✅ **GO**, 2026-06-22) Sandbox proof of Mirror additive-scenes + per-scene interest: two zones on one server, P1/P2 isolated, one A→B transition with relative placement. Output = go/no-go + a scoped list of which M1 systems need zone-awareness (the input to the M3 zone-integration item). Isolated/throwaway — modifies no shipping system. M2 content work can proceed in parallel.

## M2 — DB-Backed Content & Web Editors  *(cross-cutting; foundation shared with M1)*

> Goal: every content type is authored in a **web editor**, stored in Postgres, and loaded by the server **at startup** — no Unity rebuild to ship content.
> Path: hybrid by design — ScriptableObjects keep working until each type's DB path + web editor lands, then that type's `Resources/` load is retired. Migrate one content type at a time; nothing is thrown away.
> Sequencing within the milestone: **rails first (2.1), then items as the reference vertical (2.2), then replicate** type by type. Pure-data types are easy; types with asset/prefab/effect bindings reuse the 2.1 convention.

- [x] **2.1 — Content platform foundation (the rails).** ✅ Done 2026-06-24 ([devplan](docs/devplans/2.1-content-platform-foundation.md)). Verified end-to-end: row authored in Angular → .NET API → Postgres → Unity `ContentLoader` logged it at host start. `/api` (ASP.NET Core controllers + EF Core mapping-only, no EF Migrations — SQL runner stays sole schema authority), `/web` (Angular 21 standalone editor), `0008_content_ping.sql`, `ContentLoader`/`ContentPingRepository` seam wired into `OnStartServer`, `AssetResolver` (Addressables behind a swap seam, `com.unity.addressables` 2.6.0). Three pieces, no real content migrated yet:
  - **(a) Web stack** *(decided 2026-06-20)* — **Angular SPA + a deliberately lightweight C# (.NET) API + Postgres.** Angular chosen for the user's deep familiarity and because the complex authoring types (quest wizards, ability-effect editors) need real custom UI a generated panel can't give. The API is the minimum needed to feed Angular (Angular can't touch Postgres directly); kept thin. **No auth for now** — trusted users only; revisit when hosted/remote. The C# API may later share a model/enum library with Unity to kill type-drift. *Still open for the devplan:* where the shared content DB lives (see below), API shape/endpoints, hosting.
  - **(b) Unity DB-content loader** — a generic "load-all-into-registry on `OnStartServer`" pattern that the existing registries (`ItemRegistry`, `AbilityRegistry`, …) adopt, swapping the source behind their current lookup-by-id API.
  - **(c) Asset-binding convention** — define the string-id/path scheme for prefabs/icons/meshes/anims and the Resources/Addressables resolver, so content rows can reference Unity assets without storing them.
- [x] **2.2 — Items (reference content type).** ✅ Done 2026-06-26 ([devplan](docs/devplans/2.2-items-content-type.md)). First end-to-end vertical: `items` table + migration, server loader replacing `ItemRegistry`'s Resources path + Mirror catalog sync to clients, web **Item Editor** (identity, stats, equip slot + bonuses, weapon stats, economy). Client catalog-sync verified via the shopkeeper test (the DB-backed Tunic resolves on the client in the vendor window). Everything after copies this shape.

> **NPC content cluster (2.3–2.7), re-sequenced 2026-06-24** — pulled ahead of quests/abilities/races so a fully **web-authored vendor NPC** (and then a hostile mob) can be tested. One shared [devplan](docs/devplans/2.3-npc-content-cluster.md) covers all five (same 2.2 pattern; most are server-only — no client sync). Build/test order reaches a shopkeeper fastest: vendors → conversations → mob-slice (▶ test shopkeeper) → factions → loot (▶ test hostile mob).

- [x] **2.3 — Vendor inventories.** ✅ Done 2026-06-26 ([devplan](docs/devplans/2.3-npc-content-cluster.md)). `vendor_inventories` (+ items child) → DB + web **Vendor Editor**; server load; retired the `VendorInventory` SO path. Verified via the shopkeeper test (stock pushed to client on shop-open).
- [x] **2.4 — Conversation keyword sets.** ✅ Done 2026-06-26 ([devplan](docs/devplans/2.3-npc-content-cluster.md)). `conversation_sets` (+ keywords + unlock lists; faction gate, responses, modes) → DB + web **Conversation Editor**; server load. Verified (keyword heard → response delivered → vendor opened). *(Found a sharp edge: a vendor-open keyword must have `ends_conversation = false`, else the shop closes in the same call — see devplan log.)*
- [x] **2.5 — Mobs & NPC wiring.** ✅ Done 2026-06-26 ([devplan](docs/devplans/2.3-npc-content-cluster.md)). `mobs` table (all `MobDefinition` fields) → DB + web **Mob Editor**, new `MobRegistry`, `SpawnPoint.mobId` resolves from it. Verified: a web-authored Merchant spawned and worked as a shopkeeper in-game. *(`VendorApplicator` added to the shared `Enemy` prefab's patch list so any mob with a `vendorId` can vend.)*
- [x] **2.6 — Factions.** ✅ Done 2026-06-26 ([devplan](docs/devplans/2.6-factions.md)). `factions` (+ relations, race→faction defaults, the shared named-threshold table) → DB + web **Faction Editor**; server load (before mobs); wired mob `faction_id` + conversation `required_faction`/`required_standing` (now dropdowns). Populating `FactionRegistry` flips the already-wired mob-aggro + conversation gates live. **Faction scores re-keyed name→`faction_id` (DF2)** incl. the `character_faction_scores` persistence column. *(Deeper faction-consequence verification — aggro/gate/round-trip — deferred by the user to when faction hits on mob-death + quest completion get wired, 3.2+.)*
- [x] **2.7 — Loot tables + XP thresholds.** ✅ Done 2026-06-29 ([devplan](docs/devplans/2.7-loot-xp.md)). `loot_tables` (+ item/drop-count/coin-tier children) and the XP table → DB + web editors; retired `Resources/XpTable.asset` + the Loot Table SO path to fallbacks; wired mob `loot_table_id` (now a dropdown). `Corpse` rolls the DB-resolved `mob.lootTable`; `PlayerExperience` reads the DB XP curve. Verified in-editor: web-authored Giant Rat loot drops at seeded weights, `MaxLevel == 50`, level thresholds match the DB curve. **Closes the NPC content cluster (2.3–2.7).**
- [x] **2.7.2 — Spawn tables + timers.** ✅ Done 2026-06-29 ([devplan](docs/devplans/2.7.2-spawn-tables.md)). Migrated `SpawnTable` (weighted mob entries + `groupSize`) and `SpawnTimer` (base + variance) to DB + a web **Spawn Editor**; `SpawnPoint` references a `spawn_table_id` (DB) so it keeps **weighted / timed / grouped** spawning instead of being forced onto single-mob `mobId`, with **group spawning** (DS3) via a live-set. Retired the `SpawnTable`/`SpawnTimer` SO path to a fallback. Verified in-editor: rat camp pointed at the DB "Mob Spawn Table" spawns the DB `Giant Rat` (closing the spawn-path gap that blocked 2.7/2.7.1 in-game verification).
- [x] **2.7.1 — Mob faction hits on kill.** ✅ Done 2026-06-29 ([devplan](docs/devplans/2.7.1-mob-faction-hits.md)). *(Deferred from the 2.6/2.7 discussion.)* Per-mob `(faction_id, delta)` hit list (a `mob_faction_hits` child of `mobs`) authored in the Mob Editor; `MobKillReward` applies them via `PlayerFactionScores.ModifyScore` on death + chat feedback. `MobRepository` restructured flat→header-children. Verified in-editor: killing a Giant Rat moves Vermin −10 / CityGuards +2, persists across relog on the re-keyed `faction_id` column — which **also closes 2.6's deferred aggro/gate/round-trip verification** (this is the first in-game lever to change a standing).
- [ ] **2.8 — Quests (new content type).** Quest **data model** + web **Quest Editor**: objectives, trigger keywords, faction gates, and a reward bundle (XP / items / faction / currency). Greenfield — quests don't exist yet as data. Runtime granting is **3.2** (keyword reward action); this item delivers the authorable definitions 3.2 consumes.
- [ ] **2.9 — Abilities, tags & effects.** `ability_definitions` + `ability_tags` + cooldown links + the ordered effect list (effect-composition model preserved; effects referenced by type + params). Web **Ability Editor** + **Ability Tag Editor**. Uses the 2.1 convention for anim-trigger + prefab bindings.
- [ ] **2.10 — Races & classes.** Stat tables, XP modifiers, HP/mana formula fields, starting-ability lists → DB + web **Race & Class Editor**. Feeds `SetRaceClass` and character creation (1.5) once migrated.
- [ ] **2.11 — Seed / export / content versioning.** Reproducible DB seed, content export/import (sharing content between dev DBs / promoting to AWS), and a content-change audit trail as the library grows.
- [ ] **2.12 — Skill system + Skill Editor** *(design-first; new system, not just an editor).* **EQ-style use-based skills** (chosen 2026-06-26): per-player skill values (1H Slash, Dodge, Parry, Block, Channeling, …) that rise with use and gate combat success rolls. **Two halves:** (a) *content* — `SkillDefinition` (id, category, per-class caps, rise-chance curve, governing stat) → DB + web **Skill Editor**, fits the M2 pattern; (b) *runtime* — per-player skill values (persisted, M1-style), rise-on-use, and wiring into the combat rolls (fills the deferred **Dodge/Parry/Block** + the `Critical → Solid until skill unlocked` hook in `PlayerAutoAttack`) — this half is **combat-depth work (M4-flavored)**. Needs a **design devplan** (the skill list, rise/cap math, how each gates a roll) before any editor or wiring. No skill scaffolding exists today.

## M3 — World & Content  *(depends on M1 + M2)*

> Goal: a real place to play, built with the tools above.
> Sequencing (revised 2026-07-04): the **zone framework comes first (3.0)** so the starting zone is
> built as a proper zone from day one (not retrofitted). The **full M1 regression is deferred to
> pre-first-deployment** (2.x + 3.0 already re-covered most of it); light smoke is the interim bar.

- [x] **3.0 — Zone integration.** ✅ Code-complete + Stages A/B/C all verified in-editor (2026-07-04). The real, production version of the 2.0 spike: made the world zone-aware — additive zone scenes, per-scene interest, player→zone assignment, transitions with relative placement, persisted zone id, and zone-aware spawn/chat/AI/regen. Scoped by the 2.0 findings. Two latent bugs found + fixed en route (see 3.0.1: mob NetworkTransform, thornwood navmesh persistence). *(The full zone-aware M1 regression is deferred to pre-first-deployment, not a blocker here — see the M3 sequencing note above.)*
- [ ] **3.0.1 — Zone-integration bug follow-ups.** Bugs/gaps surfaced during 3.0 verification (see the Stage B log in `docs/devplans/3.0-zone-integration.md`). Not zone-specific; parked here because that's where they were found. Items:
  - **Remote player rotation not visible** — `Player.prefab` `NetworkTransformReliable` has `syncRotation: 0`, so observers see remote players facing their spawn orientation. Fix: sync yaw to observers (cheap path: a yaw `SyncVar` applied to the mesh, preserving the client-side instant-camera feel; avoid full rotation sync). *Real gameplay gap — schedule a fix.*
  - **MPPM keyboard focus (testing quirk, no fix)** — only the OS-focused Game view receives WASD (new Input System reads global keyboard); mouse routes per-window. Documented so it isn't re-diagnosed as a bug.
  - **MPPM "2 event systems" warning (benign, no fix)** — one EventSystem authored in `UI.unity`; each virtual player loads it, so 2+ clients trip Unity's warning. Extras auto-disable. Documented only.
  - **Per-zone bind points** — bind points are authored/persisted in starter-zone coords only; there's no per-zone bind or `/bind` yet. Stage C works around it by respawning a non-starter-zone death at that zone's `default` entry. Fix: track a bind zone id alongside the bind point (schema + `/bind`) so a cross-zone death returns to the player's actual bind. *Real gap — schedule.*
  - **Mob NetworkTransform missing (✅ FIXED + VERIFIED 2026-07-04)** — the Enemy prefab had **no NetworkTransform**, so server-side mob movement (EnemyAI/NavMeshAgent) never synced to remote clients; on non-host clients mobs looked frozen at spawn while the server AI chased/attacked (attacks read "out of range" because the server measured to the mob's real, moved position). Masked for months by host-only testing. Fix: `SceneSetup` now adds a server-authoritative `NetworkTransformReliable` (position-only, `coordinateSpace = World`) to the Enemy prefab in both build + patch paths; `EnemyAI.Awake` disables the NavMeshAgent so it only drives the transform on the server. Verified: mobs sync + are killable on remote clients in both zones.
  - **ZoneSetup scripted navmesh bake doesn't persist (✅ FIXED + VERIFIED 2026-07-04)** — zone mobs spawned off-navmesh and couldn't wander/path because the scripted `NavMeshSurface.BuildNavMesh()` didn't save the NavMeshData asset (and re-running the tool wiped any manual bake). Fix: `ZoneSetup.BakeAndPersistNavMesh` (now generalized across all built zones in 3.0.2) saves the baked NavMeshData to `Assets/Scenes/Zones/<scene>_navmesh.asset` and keeps the surface pointed at it so it reloads with the scene. Verified: thornwood + grukmar mobs wander. (Note: the earlier "creslins froze when thornwood baked" ping-pong was NOT a navmesh conflict — it was the missing-NetworkTransform bug above; the offset zone navmeshes coexist fine.)
- [x] **3.0.2 — Zone authoring tooling + 3-zone MVP scaffold.** ✅ Verified in-editor 2026-07-04. Devplan `docs/devplans/3.0.2-zone-authoring.md`. Delivered: drag-in `ZonePortal`/`ZoneWaypoint` prefabs with Scene-view labels, generalized `BuildFlatZone` + `Tools/Zones/New Zone…` workflow (flat ground + persisted navmesh + default entry + catalog/build-settings registration), intra-zone portal support (`ServerMovePlayer`), and the **3 MVP zones as flat scaffolds** (Creslin's Field ↔ Thornwood ↔ Grukmar's Deep, linear graph, ~280u each). Decoration deferred to 3.1.
- [ ] **3.1 — New-player onboarding vertical.** First polished vertical slice: a new player registers → logs in → creates a character (2 races × 3 classes) → enters a starting location, through a styled, transition-driven client shell (fades, not snaps). The character-identity hooks here feed quests later. **Combat/ability depth is intentionally minimal (1 signature ability per class) — real depth lands in M4 (combat refinement, then a level 1→2 polish round).** Two tracks (A = client UX, B = character identity) converge at character select + spawn. Each sub-item gets its own devplan.
  - [x] **3.1.1 — Client screen-flow shell + transitions** *(foundation; everything below plugs in)*. ✅ Verified in-editor 2026-07-04. Devplan `docs/devplans/3.1.1-client-screen-flow-shell.md`. Runtime-constructed uGUI shell (`Assets/Scripts/UI/Screens/`): `UIScreenManager` state machine (Title → Register/Login → Connecting → Character Select → In-World) deriving the target screen from Mirror state, `ScreenFader` **fade transitions**, and uGUI panels reusing the same Mirror messages + `AccountAuthenticator` hooks (retired IMGUI `LoginUI`/`CharacterSelectUI` disabled at runtime) — delivers the uGUI conversion of login/select deferred in 1.7. Also added a menu camera (no pre-host "no cameras" placeholder), a Log Out path, and mouse-free keyboard nav (Tab/Enter/Esc). **Verification also closed out 1.4's never-run multi-client auth matrix, fixing latent bugs:** single-login self-heal (a stale `_onlineByAccount` entry no longer locks out a valid re-login), client character-select handlers now **re-register on every reconnect** (Mirror clears `NetworkClient.handlers` on disconnect → a persistent guard left the character-list reply unhandled → silent disconnect on relogin), and login feedback text is cleared per-attempt/on-success (no stale "Success"). Styling of these screens is 3.1.2/3.1.3.
  - [ ] **3.1.2 — Title / start page.** Entry screen (Play / Quit; Settings later).
  - [ ] **3.1.3 — Register + Login screens.** Styled uGUI on the shell (replaces the IMGUI `LoginUI`).
  - [ ] **3.1.4 — Race models + animation (Human, Elf).** Synty models on Humanoid rigs so locomotion + existing ability anims retarget; wire race → in-world model + char-select preview. *(User imports the Synty pack for the elf.)*
  - [ ] **3.1.5 — Class setup (Warrior, Wizard, Cleric).** Configure stats/HP/mana + **1 signature ability each** (Warrior Kick exists; Wizard nuke; Cleric heal) — just enough to feel distinct. Depth deferred to M4.
  - [ ] **3.1.6 — Character select/create redesign.** Rearranged + styled, with a **live 3D character preview** reflecting race/class.
  - [ ] **3.1.7 — Camp / logout polish.** Smooth fade back to select/title (no snap), tidy session teardown.
  - [ ] **3.1.8 — Light starting-location polish.** Just enough Synty dressing around the Creslin's Field spawn that the entry point looks intentional (full multi-zone decoration is 3.6).
- [ ] **3.2 — Quest rewards via keywords.** `IOnConversationKeyword` action granting XP/items/faction, faction-gated, no quest UI. *(Consumes quest definitions authored in 2.3.)*
- [ ] **3.3 — NPC item-giving.** Trade item to NPC via keyword; server validates, removes, fires script event.
- [ ] **3.4 — Real aggro / threat.** Replace placeholder: faction-driven perception + social aggro + threat decay.
- [ ] **3.5 — Second zone (content).** A real second zone built on the 3.0 framework (the transition architecture now lives in 3.0); proves the framework with actual content + defines what state crosses zone boundaries in practice.
- [ ] **3.6 — Zone content & decoration pass.** (Was the original 3.1.) Decorate + populate the 3.0.2 scaffolds across all zones: hand-placed Synty dressing, mobs/spawns, named NPCs, points of interest — using the 3.0.2 tooling.

## M4 — Gameplay Depth

> Goal: make the loop genuinely fun.

- [ ] **4.1 — Combat refinement.** *(User-flagged as the next depth effort after the 3.1 onboarding vertical.)* Tighten the combat model and flesh out the 3.1 classes beyond their single signature ability — hit/avoidance/damage feel, ability kits, cooldown/resource tuning.
- [ ] **4.2 — Level 1→2 polish round.** A focused vertical polishing the earliest gameplay progression (level 1 → 2) end to end: abilities, combat feel, feedback, pacing — the payoff of the combat refinement on the real starting experience.
- [ ] **4.3 — Player grouping.** Parties (≤6), shared XP, group health frames, friendly-fire rules.
- [ ] **4.4 — Group threat/aggro tuning.**
- [ ] **4.5 — More abilities per class.** *(Largely folded into 4.1/4.2; keep for the broader spellbook fill-out.)*
- [ ] **4.6 — Goals / win-lose conditions.**
- [ ] **4.7 — Balance pass** against real play data.

## M5 — Deploy & Scale  *(AWS)*

> Goal: move from local hobby setup toward a real server.

- [ ] **5.1 — Deployed Postgres on AWS** (migrate from local Docker).
- [ ] **5.2 — Dedicated server build + hosting.**
- [ ] **5.3 — Transport security + real auth** (the deferred security work: encryption, hardened login).
- [ ] **5.4 — Build pipeline / launcher / patcher.**
- [ ] **5.5 — Load testing + zone-server architecture.**

---

## Devplan log

Implemented items and their devplans are listed here as we go (newest first).

- **2.7 — Loot tables + XP thresholds (DB-backed + web Loot/XP editors)** — ✅ 2026-06-29 — closes the NPC content cluster (2.3–2.7) — [devplan](docs/devplans/2.7-loot-xp.md)
- **2.7.1 — Mob faction hits on kill (per-mob `(faction_id, delta)` list + `MobKillReward` wiring)** — ✅ 2026-06-29 — [devplan](docs/devplans/2.7.1-mob-faction-hits.md)
- **2.7.2 — Spawn tables + timers (DB-backed + web Spawn Editor; group spawning)** — ✅ 2026-06-29 — [devplan](docs/devplans/2.7.2-spawn-tables.md)
- **2.6 — Factions (DB-backed + web Faction Editor; scores re-keyed to faction_id)** — ✅ 2026-06-26 — [devplan](docs/devplans/2.6-factions.md)
- **2.2–2.5 — Items + NPC content cluster (vendors / conversations / mobs)** — ✅ 2026-06-26 — verified end-to-end by a web-authored shopkeeper — [items devplan](docs/devplans/2.2-items-content-type.md) · [cluster devplan](docs/devplans/2.3-npc-content-cluster.md)
- **2.1 — Content platform foundation (the rails)** — ✅ 2026-06-24 — [devplan](docs/devplans/2.1-content-platform-foundation.md)
- **1.6.1 — Camp & session polish** — ✅ 2026-06-22 — [devplan](docs/devplans/1.6.1-camp-session-polish.md)
- **1.7 — UI architecture refactor** — ✅ 2026-06-21 — [devplan](docs/devplans/1.7-ui-architecture-refactor.md)
- **1.6 — Save policy + multi-character** — ✅ 2026-06-21 — [devplan](docs/devplans/1.6-save-policy-multichar.md)
- **1.5 — Character select + creation (first-run)** — ✅ 2026-06-21 — [devplan](docs/devplans/1.5-character-select-creation.md)
- **1.3 — Character-state schema + save/load round-trip** — ✅ 2026-06-21 — [devplan](docs/devplans/1.3-character-state-persistence.md)
- **1.4 — Accounts + login** — ✅ 2026-06-21 — [devplan](docs/devplans/1.4-accounts-login.md) *(implemented before 1.3 per review decision O1)*
- **1.2 — Server-side data-access layer + async save queue** — ✅ 2026-06-20 — [devplan](docs/devplans/1.2-data-access-layer.md)
- **1.1 — Postgres + Docker dev environment** — ✅ 2026-06-20 — [devplan](docs/devplans/1.1-postgres-docker-env.md)
