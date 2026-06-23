# M1 Regression Checklist

Broad verification pass over everything M1 touched (1.1–1.7 + 1.6.1) before starting M2. M1 rewired
persistence, accounts, character flow, and the entire UI layer, so this sweeps the whole loop end to
end. Run it after a clean recompile; use **Multiplayer Play Mode (MPPM)** for the multi-client cases.

**Setup before starting:**
- [ ] Postgres up (`docker compose up -d`); `Tools/Database/Run Migrations` → "no pending" (0001–0007 applied).
- [ ] Clean recompile, no console errors/warnings (watch for duplicate EventSystem, missing refs).
- [ ] `UI.unity` exists + in Build Settings; gameplay scene has `UIManager`, no stale HUD canvases.
- [ ] (Optional) `Tools/Database/Create Account` a couple of test accounts, or use register in-client.

---

## 1. Database & migrations (1.1/1.2)
- [ ] Host start logs `[DB] Connected…` + schema up to date; `PersistenceService started`.
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
- [ ] Duplicate character name → refused ("name already taken"); too-short name → refused.
- [ ] Account isolation: account B never sees account A's characters.
- [ ] No `[Persist] … prefab default race/class fallback` warning in the normal flow.

## 4. Character persistence round-trip (1.3)
- [ ] Gain XP, pick up + **equip** an item, move currency, rearrange a hotbar slot, walk to a distinct spot.
- [ ] Stop → restart → **Enter** → everything restores: XP/level, inventory, equipment, currency, hotbar
      arrangement, current HP/mana, position.
- [ ] **Equipment bonus applied exactly once** (record a stat with the item on, relog, confirm identical).
- [ ] Derived state recomputed (Max HP/mana match the formula for level+stats; known abilities from class).
- [ ] Bind point survives: die → respawn at bind → relog → bind intact.

## 5. Multiple characters + camp + autosave (1.6)
- [ ] Create A → **camp** → create B → both in the roster; 8-slot cap disables Create when full.
- [ ] Play A (gain XP), camp, play B → independent; relog → **both persist with their own state**
      (no cross-contamination, no duplicate rows — the core re-keying risk).
- [ ] Delete a character → roster updates → re-create works.
- [ ] **Autosave:** play, gain XP, **hard-kill the editor** (simulate crash) → relog → progress up to the
      last ~90s tick survives.
- [ ] Save-on-quit: normal editor Stop → latest state saved.

## 6. Camp & session polish (1.6.1)
- [ ] `/help` lists every command; each listed command works.
- [ ] `/camp` and the HUD **Camp** button both run the 10s countdown → return to select.
- [ ] **Combat gate + indicator:** take/deal damage → HP-frame border pulses red **and** camp refused;
      ~10s after combat clears → border off, camp works. Server refuses a camp that completes in combat.
- [ ] Camp **cancels** on movement (and on entering combat) with a message.
- [ ] Chat **clears** on character switch; **MOTD** ("Welcome to Ueq, <name>!") shows on entering.

## 7. UI architecture (1.7) — the broad-blast-radius item
- [ ] HUD loads from the additive `UI.unity`; single EventSystem (no "multiple EventSystems" warning).
- [ ] **LocalPlayer rebinding:** camp → panels clear; re-enter → repopulate; switch characters → panels
      reflect the new one.
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

## 9. Editor tooling sanity
- [ ] `Tools/Setup All`, `Tools/Build UI Scene`, `Tools/Patch Player Prefab` run clean.
- [ ] `Tools/Database/*` (Test Connection, Run Migrations, Create Account, Save Character Now, Wipe Character).
- [ ] Mob / Item / Ability / Race & Class / Loot Table / Vendor editors open and save.

---

## Outcome
- [ ] All boxes pass → **M1 closed; proceed to M2** (DB-backed content + web editors).
- [ ] Log any failures here with repro, fix or file as a follow-up item before M2.
