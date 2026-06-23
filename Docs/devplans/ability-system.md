# Dev Plan: Ability System

**Status:** Planned — not yet implemented  
**Phase:** 2 ("Make Characters Matter")  
**Session:** 2026-05-24

---

## Goals

- Data-driven: new ability = new ScriptableObject asset; new mechanic = new `AbilityEffect` subclass; no existing code touched
- Generic tagging system that serves multiple purposes (cooldown linking, elemental identity, combo triggers, etc.)
- Warrior abilities cost 0 mana; balance comes from linked cooldown groups instead of a mana gate
- Instant casts only for MVP (cast bar deferred)
- MVP effects: `DamageEffect`, `HealEffect`; all others follow the same pattern

---

## Part 1 — Data Layer

### `AbilityTag` (ScriptableObject, `Assets/Ueq/Ability Tag`)

Pure semantic label. No behavior baked in. Systems that care about tags read them; the tag itself stays dumb.

```
tagId        string
displayName  string
```

**Asset location:** `Assets/ScriptableObjects/AbilityTags/`

**Example tags:**
- `MartialAbility` — shared by warrior abilities; used in cooldown links
- `Fire` — semantic label; future combo/proc systems check for it
- `Healing` — semantic; could gate faction reactions or proc buffs
- `Slash`, `Strike`, `Ward` — ability-type sub-tags for fine-grained linking

---

### `CooldownLink` (serializable struct, inline in `AbilityDefinition`)

```csharp
[Serializable]
public class CooldownLink
{
    public AbilityTag tag;
    public float duration;
}
```

When an ability is cast, each of its `CooldownLink` entries starts the corresponding tag's shared timer for `duration` seconds. Different abilities can set the same tag's timer to different durations (e.g., a big warrior ability sets `MartialAbility` to 10s; a quick one sets it to 3s).

---

### `AbilityEffect` (abstract ScriptableObject, `Assets/Ueq/Ability Effect`)

```csharp
public abstract class AbilityEffect : ScriptableObject
{
    public abstract void Apply(NetworkIdentity caster, NetworkIdentity target, AbilityDefinition source);
}
```

**MVP concrete subclasses:**

| Class | Fields | Mechanic |
|---|---|---|
| `DamageEffect` | `baseDamage`, `scalingStat` (enum: Str/Dex/Int/Wis/None), `scalingFactor` | Calls `Health.TakeDamage` |
| `HealEffect` | `baseHeal`, `scalingStat`, `scalingFactor` | Calls `Health.RestoreHealth` (stub added to Health) |

**Deferred subclasses** (same pattern, add when needed):
`DotEffect`, `HotEffect`, `BuffEffect`, `DebuffEffect`, `SnareEffect`, `RootEffect`, `StunEffect`, `FactionHitEffect`

---

### `AbilityDefinition` (ScriptableObject, `Assets/Ueq/Ability Definition`)

```
abilityId        string
displayName      string
description      string
targetingType    enum { Self, SingleTarget }   // AoE deferred
range            float
castTime         float                          // 0 = instant; cast bar deferred
manaCost         int                            // 0 for warrior abilities
tags             List<AbilityTag>               // semantic labels
cooldownLinks    List<CooldownLink>             // empty = use GCD
effects          List<AbilityEffect>            // applied in order on server
```

**Asset location:** `Assets/ScriptableObjects/Abilities/`

---

### `ClassDefinition` addition

```
startingAbilities    List<AbilityDefinition>
```

Granted to the player on `SetRaceClass`. Populates `PlayerAbilities._knownAbilities`.

---

## Part 2 — Cooldown Model

### GCD rule

- `cooldownLinks` is **empty** → ability uses the Global Cooldown (default 1.5s)
- `cooldownLinks` is **non-empty** → ability uses those specific tag timers; GCD does **not** apply

GCD and linked timers are mutually exclusive per ability.

### Server-side timer state (in `PlayerAbilities`)

```
float _gcdTimer                          // counts down each Update
Dictionary<string, float> _linkedTimers  // keyed by tag.tagId
```

### `IsOnCooldown(AbilityDefinition ability)`

- No `cooldownLinks` → `_gcdTimer > 0`
- Has `cooldownLinks` → any `_linkedTimers[link.tag.tagId] > 0`

### On successful cast

- No `cooldownLinks` → `_gcdTimer = _gcdDuration` (const, e.g. 1.5f)
- Has `cooldownLinks` → foreach link: `_linkedTimers[link.tag.tagId] = link.duration`

### UI cooldown value per hotbar slot

- GCD ability → `_gcdTimer`
- Linked ability → `Max(remaining time across all this ability's linked tag timers)`

This value is written to `SyncList<float> _hotbarCooldowns` on the server each frame so the client UI can display the overlay without needing to know tag internals.

### Example: warrior kit

```
"Overhead Slash"
  tags:          [MartialAbility, Slash]
  cooldownLinks: [{ MartialAbility, 10s }, { Slash, 3s }]

"Brutal Strike"
  tags:          [MartialAbility, Strike]
  cooldownLinks: [{ MartialAbility, 10s }, { Strike, 5s }]

"War Cry"
  tags:          [MartialAbility]
  cooldownLinks: [{ WarCry, 30s }]     // independent — no shared MartialAbility link
```

Casting "Overhead Slash" starts `MartialAbility(10s)` and `Slash(3s)`. "Brutal Strike" is blocked for 10s by `MartialAbility`. "War Cry" is unaffected.

---

## Part 3 — Runtime Layer

### `AbilityRegistry` (MonoBehaviour singleton)

Follows `ItemRegistry` pattern exactly. Loads all `AbilityDefinition` assets from `Resources/Abilities/` at `Awake`. `Get(string id)` lookup.

---

### `PlayerAbilities` (NetworkBehaviour)

**Synced storage:**
```
SyncList<string> _hotbar          // 8 slots, "" = empty
SyncList<string> _knownAbilities  // populated from ClassDefinition on SetRaceClass
SyncList<float>  _hotbarCooldowns // one float per slot, written from server for UI
```

**Server-only:**
```
float _gcdTimer
float _gcdDuration = 1.5f
Dictionary<string, float> _linkedTimers
```

**Key methods:**
- `SetRaceClass(ClassDefinition cls)` — clears + repopulates `_knownAbilities`
- `[Server] SetHotbarSlot(int slot, string abilityId)` — validates known, writes `_hotbar`
- `[Server] TryCast(int hotbarSlot, NetworkIdentity target)` — full pipeline (see below)
- `IsOnCooldown(AbilityDefinition)` — see model above
- `Update()` (server only) — decrement `_gcdTimer` and all `_linkedTimers` entries; write `_hotbarCooldowns`

**Casting pipeline:**
1. Slot not empty; ability known; not on cooldown
2. Target exists and in range (Euclidean; same LOS check as `PlayerAutoAttack`)
3. `PlayerMana.HasMana(cost)` — if not: `ChatManager.SendDirect(Ability, "Not enough mana.")` → return
4. `PlayerMana.UseMana(cost)`
5. Foreach effect: `effect.Apply(caster, target, ability)`
6. Start cooldown (GCD or linked timers)
7. `ChatManager.SendDirect(Ability, "You cast [Name].")` to caster

---

### `NetworkedPlayer` additions

```csharp
[Command] void CmdCastAbility(int hotbarSlot, NetworkIdentity target)
    => _playerAbilities.TryCast(hotbarSlot, target);

[Command] void CmdSetHotbarSlot(int slot, string abilityId)
    => _playerAbilities.SetHotbarSlot(slot, abilityId);
```

---

### `PlayerExperience.SetRaceClass` forward

Same pattern as existing `CharacterStats.SetRaceClass` call — add `_playerAbilities.SetRaceClass(cls)`.

---

### `Health.RestoreHealth` stub

```csharp
[Server]
public void RestoreHealth(int amount)
{
    _current = Mathf.Min(_current + amount, EffectiveMax);
}
```

---

## Part 4 — UI Layer

### `HotbarUI` (MonoBehaviour on HotbarCanvas)

- 8 slots, keys **2–9** (key 1 stays as autoattack toggle)
- Per slot: ability name label, cooldown countdown text, semi-transparent grey overlay when `_hotbarCooldowns[i] > 0`
- On key press: read `_hotbar[slot]`, call `CmdCastAbility(slot, currentTarget)`
- Late-binds to local player's `PlayerAbilities` on first `Update` (same pattern as `InventoryUI`)
- No drag-to-hotbar for MVP — abilities pre-assigned via `SceneSetup` or editor inspector
- Additional hotbars deferred — architecture leaves room (hotbar index can extend beyond 8)

---

## Part 5 — Editor Tooling

### `AbilityTagEditorWindow` (`Tools/Ability Tag Editor`)

- Lists all `AbilityTag` assets in `Assets/ScriptableObjects/AbilityTags/`
- Edit `tagId` + `displayName` inline
- Create / delete buttons

### `AbilityEditorWindow` (`Tools/Ability Editor`)

- Left panel: list of `AbilityDefinition` assets in `Assets/ScriptableObjects/Abilities/`
- Right panel sections:
  - **Identity** — id, name, description
  - **Targeting** — targeting type, range, cast time, mana cost
  - **Tags** — multi-select from known `AbilityTag` assets
  - **Cooldown Links** — list of `{ AbilityTag, duration }` pairs; add/remove inline
  - **Effects** — ordered list of `AbilityEffect` subclass assets; add/remove/reorder
- Create button: new asset in `Assets/ScriptableObjects/Abilities/`

### Race & Class Editor addition

- "Known Abilities" list on the Classes tab, backed by `ClassDefinition.startingAbilities`
- `AbilityDefinition` asset picker inline

---

## Part 6 — SceneSetup Wiring

- `Patch Player Prefab`: add `PlayerAbilities`
- `Setup All`: create `AbilityRegistry` GameObject, create `HotbarCanvas` with 8 slots

---

## Implementation Order

| Step | Deliverable | Notes |
|---|---|---|
| 1 | `AbilityEffect` (abstract) + `DamageEffect` + `HealEffect` | Data only |
| 2 | `AbilityTag` ScriptableObject | Pure label |
| 3 | `CooldownLink` struct + `AbilityDefinition` | Depends on 1–2 |
| 4 | `AbilityRegistry` | Mirrors `ItemRegistry` |
| 5 | `Health.RestoreHealth` stub | Needed by `HealEffect` |
| 6 | `ClassDefinition.startingAbilities` | One-field addition |
| 7 | `PlayerAbilities` (GCD + linked timer engine) | Core runtime |
| 8 | `NetworkedPlayer` Cmd wiring | Thin forwarders |
| 9 | `PlayerExperience.SetRaceClass` forward | Wire into existing call |
| 10 | `HotbarUI` (2–9, cooldown overlay per slot) | Depends on PlayerAbilities |
| 11 | `AbilityTagEditorWindow` (`Tools/Ability Tag Editor`) | Tooling |
| 12 | `AbilityEditorWindow` (`Tools/Ability Editor`) | Tooling |
| 13 | Race & Class Editor: known abilities list | Tooling |
| 14 | SceneSetup patch + Setup All wiring | Integration |
| 15 | Create 3 test abilities + assign to a class | Smoke test |

---

## Deferred

- Cast bar (timed casts) — instant-only for MVP
- AoE / Cone targeting
- Drag-to-hotbar from ability book UI
- Additional hotbar pages
- Resist/saving throw system for spells
- Shared GCD display animation (sweep overlay)
- `DotEffect`, `HotEffect`, `BuffEffect`, `DebuffEffect`, `SnareEffect`, `RootEffect`, `StunEffect`, `FactionHitEffect`
- Heroic opportunity / combo proc system (tags provide the foundation)

---

## Open Design Questions

- **Cooldown sync granularity** — writing `_hotbarCooldowns` every server frame is simple but chatty. Alternative: write on change + let clients interpolate. Deferred.
- **Spell damage formula** — should spell damage use the same ATK/AGI hit-roll system as autoattack, or always hit (with resist/saving throw later)? Deferred.
