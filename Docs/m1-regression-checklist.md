# M1 Regression Checklist  *(run after 3.0 — zone-aware state)*

Broad verification pass over everything M1 touched (1.1–1.7 + 1.6.1) before starting M2. M1 rewired
persistence, accounts, character flow, and the entire UI layer; **3.0 then reworked persistence, spawn,
chat, player-spawn, and UI-across-scenes to be zone-aware**, which is exactly why this full sweep is
scheduled to run *after* 3.0 — it validates the post-rework state once instead of twice. Run it after a
clean recompile; use **Multiplayer Play Mode (MPPM)** for the multi-client cases.

**Known quirks — do NOT file these as regressions** (tracked in roadmap **3.0.1**):
- Remote players don't visibly rotate (`syncRotation: 0`, intentional — rotation is client-side).
- With **2+ MPPM clients**, the "there are 2 event systems in the scene" warning is expected/benign (each
  virtual player loads `UI.unity`; Unity auto-disables extras). A **single** client should be clean.
- MPPM keyboard focus: only the focused Game view receives WASD; click into a client's view to drive it.

**Setup before starting:**
- [ ] Postgres up (`docker compose up -d`); `Tools/Database/Run Migrations` → "no pending" (**0001–0017** applied).
- [ ] `Tools/Database/Seed Database` (or Host once) → content seeded; mobs incl. **Giant Rat** resolve.
- [ ] Clean recompile, no console **errors** (ignore the benign multi-client EventSystem warning above).
- [ ] `UI.unity` exists + in Build Settings; gameplay scene has `UIManager`, no stale HUD canvases.
- [ ] **Zones built** (`Tools/Zones/Build Zone Scenes` has been run + SampleScene saved):
  - `Resources/ZoneCatalog.asset` exists (`creslins_field` base + `thornwood`); thornwood in Build Settings + navmesh baked.
  - NetworkManager GO has **`ZoneManager` + `ZoneInterestManagement`** (not the raw `SceneInterestManagement`).
  - SampleScene has the creslins→thornwood portal + return entry; thornwood has the **Giant Rat spawn**.
- [ ] (Optional) `Tools/Database/Create Account` a couple of test accounts, or use register in-client.

---

## 1. Database & migrations (1.1/1.2) + zone boot (3.0)
- [ ] Host start logs `[DB] Connected…` + schema up to date; `PersistenceService started`.
- [ ] `[Content] Loaded …` lines (items/mobs/etc.) and `[Zone] Loaded zone 'thornwood' (thornwood) at offset (5000…)`.
- [ ] Stop host logs `PersistenceService stopped (queue flushed)`.
- [ ] DB-down path: `docker compose stop` → host start aborts with the loud error (don't run without DB).

## 2. Accounts & login (1.4) — MPPM
- [ ] Host as **dev** → auto-login works.
- [ ] Player 2: **Register** a new account → connects.
- [ ] Wrong password (≥4 chars) → "invalid username or password."
- [ ] Duplicate username on Register → "username taken."
- [ ] Same account already online (2nd client) → "already online"; frees after first disconnects.
- [ ] Console shows the account→connection mapping when a player is added.

## 3. Character select & creation (1.5)
- [ ] Fresh account → **create form** appears (no characters).
- [ ] Create name/race/class → spawns into world as that character; nameplate + chat identity = the name.
- [ ] New character spawns in the **starter zone** (`creslins_field`); a save writes `zone_id = creslins_field`.
- [ ] Duplicate character name → refused ("name already taken"); too-short name → refused.
- [ ] Account isolation: account B never sees account A's characters.
- [ ] No `[Persist] … prefab default race/class fallback` warning in the normal flow.

## 4. Character persistence round-trip (1.3) + zone id (3.0)
- [ ] Gain XP, pick up + **equip** an item, move currency, rearrange a hotbar slot, walk to a distinct spot.
- [ ] Stop → restart → **Enter** → everything restores: XP/level, inventory, equipment, currency, hotbar
      arrangement, current HP/mana, position.
- [ ] **Equipment bonus applied exactly once** (record a stat with the item on, relog, confirm identical).
- [ ] Derived state recomputed (Max HP/mana match the formula for level+stats; known abilities from class).
- [ ] Bind point survives: die → respawn at bind → relog → bind intact.
- [ ] **`zone_id` round-trips:** while in `creslins_field`, relog → back in creslins at saved position; the
      DB `characters.zone_id` reads `creslins_field`. (Cross-zone login is covered in §9.)

## 5. Multiple characters + camp + autosave (1.6)
- [ ] Create A → **camp** → create B → both in the roster; 8-slot cap disables Create when full.
- [ ] Play A (gain XP), camp, play B → independent; relog → **both persist with their own state**
      (no cross-contamination, no duplicate rows — the core re-keying risk).
- [ ] Delete a character → roster updates → re-create works.
- [ ] **Autosave:** play, gain XP, **hard-kill the editor** (simulate crash) → relog → progress up to the
      last ~90s tick survives.
- [ ] Save-on-quit: normal editor Stop → latest state saved.

## 6. Camp & session polish (1.6.1)
- [ ] `/help` lists every command (incl. `/camp`, `/unstuck`); each listed command works.
- [ ] `/camp` and the HUD **Camp** button both run the 10s countdown → return to select.
- [ ] **Combat gate + indicator:** take/deal damage → HP-frame border pulses red **and** camp refused;
      ~10s after combat clears → border off, camp works. Server refuses a camp that completes in combat.
- [ ] Camp **cancels** on movement (and on entering combat) with a message.
- [ ] Chat **clears** on character switch; **MOTD** ("Welcome to Ueq, <name>!") shows on entering.
- [ ] **`/unstuck`** warps to the current zone's `default` entry (or bind if zones off), gated out of combat.

## 7. UI architecture (1.7) — the broad-blast-radius item
- [ ] HUD loads from the additive `UI.unity`; **single** client shows no EventSystem warning (multi-client
      warning is the known 3.0.1 quirk, not a failure).
- [ ] **LocalPlayer rebinding:** camp → panels clear; re-enter → repopulate; switch characters → panels
      reflect the new one.
- [ ] **UI survives a zone transition:** walk creslins→thornwood → the HUD does **not** reload (panels stay
      bound, no flicker — this is the reason `UI.unity` is additive, the M3 enabler).
- [ ] **Host-restart in one Play session** (Stop Host → Start Host) → all panels rebind cleanly
      (inventory interactable, target frame updates, hotbar labels present — the bug that's bitten us).
- [ ] **Hotbar labels:** all 8 slots labeled (2 Kick, 3 Taunt, 4–9 numbered) — the runtime self-wiring fix.
- [ ] Each panel parity: player/target frames, inventory move/equip/drop, equipment equip/unequip,
      hotbar cast + cooldowns, loot (RMB corpse), vendor buy/sell, chat channels + `/whisper`.

## 8. Core gameplay loop (make sure M1 didn't break the game)
- [ ] Movement/sprint/jump, RMB look, LMB click-to-target + highlight.
- [ ] Auto-attack (key 1) hits/misses; ability cast (Kick) fires + animates + respects cooldown.
- [ ] Enemy AI: aggro/chase/combat/leash; mob death → XP + loot drop; corpse loot (owner-only for players).
- [ ] Death handling: drop corpse, XP loss, respawn at bind; corpse loot recovers XP.
- [ ] Regen ticks (HP + mana) while alive and below max.
- [ ] **The full loop also works inside a non-base zone** (thornwood): fight the Giant Rat there → XP/loot/
      corpse all function exactly as in creslins.

## 9. Zone integration (3.0) — MPPM, 2 clients across zones
> Stages A/B/C from `docs/devplans/3.0-zone-integration.md`. Run **Server Only + 2 MPPM clients**; P1 stays
> in creslins, P2 walks north through the cyan pillar to thornwood.
- [ ] **Parity (Stage A/B):** the whole existing loop works in creslins with zones enabled (nothing above regressed).
- [ ] **Transition (Stage B):** creslins↔thornwood round-trip is stable — correct height, no owner-side sink,
      client additively loads/unloads the zone scene.
- [ ] **Interest isolation (Stage B):** once separated, P1 and P2 **stop seeing each other** and each other's mobs.
- [ ] **Chat isolation (3.0):** `/say` and `/shout` do **not** cross zones; `/whisper <name>` and System
      messages still deliver cross-zone; local chat still works within a zone.
- [ ] **Per-zone mobs (Stage C):** P2 in thornwood sees the Giant Rat; P1 in creslins does **not** (and creslins
      mobs are invisible to P2).
- [ ] **Corpse-into-zone (Stage C):** die in thornwood → your corpse is in thornwood (lootable there), not creslins.
- [ ] **Death respawn (Stage C):** creslins death → respawn at bind (now via `ServerTeleport`); thornwood death →
      respawn at thornwood's `default` entry, still in thornwood (cross-zone bind is the 3.0.1 gap).
- [ ] **Login into persisted zone (Stage C):** `/camp` in thornwood → re-enter → spawn back in thornwood at the
      saved position (client loads thornwood additively); DB `zone_id = thornwood`.
- [ ] **`/unstuck` in thornwood** returns to thornwood's `default` entry (not creslins).

## 10. Editor tooling sanity
- [ ] `Tools/Setup All`, `Tools/Build UI Scene`, `Tools/Patch Player Prefab`, `Tools/Zones/Build Zone Scenes` run clean.
- [ ] `Tools/Database/*` (Test Connection, Run Migrations, Seed Database, Create Account, Save Character Now, Wipe Character).
- [ ] Mob / Item / Ability / Race & Class / Loot Table / Vendor / Spawn editors open and save.

---

## Outcome
- [ ] All boxes pass → **M1 closed (validated against the zone-aware world); 3.0 verified; proceed to 3.0.1 / 3.1.**
- [ ] Log any failures here with repro; fix or file as a 3.0.1 follow-up before moving on.
