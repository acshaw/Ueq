# EVENTIDE — Combat Pipeline Reference
*June 2026 — Claude Code Reference*

> **Note:** This document supersedes `combat_math_reference.docx` where values conflict. Use the class starting tables here, not the earlier document.

---

## Table of Contents

1. [Pipeline Overview](#1-pipeline-overview)
2. [Step 1 — Hit Roll (LOCKED)](#2-step-1--hit-roll-locked)
   - 2.1 Resolution Engine
   - 2.2 Base Weighted Table
   - 2.3 Hit Tier Definitions
   - 2.4 Zero Stat Baseline
   - 2.5 Class Starting Tables
   - 2.6 Modifiers
   - 2.7 Level Differential
   - 2.8 Skill Differential
   - 2.9 Position Modifier
   - 2.10 Stat Contribution
   - 2.11 Level 20 Warrior Target Table
3. [Step 2 — Avoidance Roll (DEFINED — IMPLEMENTATION PENDING)](#3-step-2--avoidance-roll-defined--implementation-pending)
   - 3.1 Waterfall Model
   - 3.2 Avoidance Outcomes
   - 3.3 Dodge Curve
   - 3.4 Parry and Riposte
   - 3.5 Riposte Counter Attack
4. [Step 3 — Damage Application (PARTIALLY DEFINED)](#4-step-3--damage-application-partially-defined)
   - 4.1 Tier Damage Outputs
   - 4.2 Open Questions
5. [Step 4 — Mitigation (NAMED — UNDEFINED)](#5-step-4--mitigation-named--undefined)
6. [Implementation Notes](#6-implementation-notes)

---

## 1. Pipeline Overview

Every attack in Eventide resolves through four sequential steps. Each step produces an output that feeds the next. Steps are processed server-side and players observe only outputs — damage numbers and hit tier results. The underlying math is intentionally opaque.

| Step | Name | Input | Output |
|---|---|---|---|
| 1 | Hit Roll | Attacker stats, defender stats, position, level differential | Hit tier (Miss through Crippling) |
| 2 | Avoidance Roll | Hit tier from Step 1, defender agility/dexterity | Final hit tier — same or reduced to Miss |
| 3 | Damage Application | Final hit tier from Step 2, weapon base damage | Raw damage number |
| 4 | Mitigation | Raw damage from Step 3, defender armor and buffs | Final damage number applied to HP |

**Performance note:** All four steps are lightweight arithmetic against in-memory values. No database calls during resolution. Hundreds of simultaneous combat calculations per second are well within server capacity.

---

## 2. Step 1 — Hit Roll (LOCKED)

### 2.1 Resolution Engine

All to-hit resolution uses a weighted probability table. The roll is always a clean random number between 0 and the current total weight. Modifiers reshape the table before the roll — the roll itself is never touched. The table's 2d6 origin is intentionally obscured. Players observe results, not mechanics.

The modifier system transfers weight between tiers before the roll. Every modifier is a weight transfer, never a roll modifier. Total weight always sums to 340 regardless of modifiers applied.

### 2.2 Base Weighted Table

Zero stat baseline — a character with no stat investment before any class allocation:

| Tier | Base Weight | Probability | Notes |
|---|---|---|---|
| Miss | 119 | 35% | Zero stat floor |
| Glancing | 119 | 35% | Zero stat floor |
| Hit | 102 | 30% | Zero stat floor |
| Solid Hit | 0 | 0% | Requires stat investment |
| Good Hit | 0 | 0% | Requires stat investment |
| Critical | 0 | 0% | Requires stat investment |
| Crippling | 0 | 0% | Class passive unlock only |
| **Total** | **340** | **100%** | |

### 2.3 Hit Tier Definitions

| Tier | Damage Output | Availability | Notes |
|---|---|---|---|
| Miss | 0 | All classes | Attack fully avoided |
| Glancing | ~25% base | All classes | Partial contact |
| Hit | ~60% base | All classes | Clean contact |
| Solid Hit | 100% base | All classes | Well-placed strike |
| Good Hit | 100% + minor bonus | All classes | Exploits opening |
| Critical | 100% + bonus | All classes | Significant vulnerability |
| Crippling | 100% + major bonus | Passive unlock only | Martial classes only |

### 2.4 Zero Stat Baseline

The zero stat baseline is 35% Miss, 35% Glancing, 30% Hit, 0% above. All class starting tables are built by moving weight from this baseline using the boundary cost model: **0.5% movement across 1 boundary costs 1 stat point.**

### 2.5 Class Starting Tables — Level 1

These tables represent post-stat-allocation starting state at level 1:

| Tier | Warrior (90 pts) | Cleric (75 pts) | Wizard (55 pts) | Notes |
|---|---|---|---|---|
| Miss | 17.5% | 20% | 25% | |
| Glancing | 40% | 40% | 40% | Consistent across classes |
| Hit | 30% | 30% | 25% | |
| Solid Hit | 10% | 7.5% | 7.5% | |
| Good Hit | 2.5% | 2.5% | 2.5% | |
| Critical | 0% | 0% | 0% | Unlocks through progression |
| Crippling | 0% | 0% | 0% | Class passive required |
| **Total** | **100%** | **100%** | **100%** | |

Stat point allocation uses boundary cost model. Moving 0.5% across 1 boundary costs 1 stat point. Moving across multiple non-adjacent boundaries costs proportionally more. The distribution editor tool (`combat_tier_editor.html`) was used to derive these tables.

### 2.6 Modifiers

All modifiers transfer weight between tiers before the roll. The roll is always clean. No modifier directly manipulates the random number.

### 2.7 Level Differential

Uses Fibonacci threshold structure. The gap between Fibonacci thresholds defines how many level increments produce one full futility shift. At low levels a single level difference is enormous. At high levels the same absolute difference is far less dramatic.

| Level Band | Levels In Band | Increments To Futility | Notes |
|---|---|---|---|
| 1 → 2 | 1 | 1 | Full futility in one step |
| 2 → 3 | 1 | 1 | Full futility in one step |
| 3 → 5 | 2 | 2 | Futility across 2 steps |
| 5 → 8 | 3 | 3 | Futility across 3 steps |
| 8 → 13 | 5 | 5 | Futility across 5 steps |
| 13 → 21 | 8 | 8 | Futility across 8 steps |

**Futility definition:** distribution heavily favoring Miss and Glancing. Exact weight values are tuning parameters — set conservatively and adjusted through playtesting. **Asymmetry applies:** level disadvantage hurts faster than level advantage helps.

### 2.8 Skill Differential

Net value: attacker weapon skill minus defender weapon skill. Range within a level band is -5 to +5 (skill cap increases by 5 per level). Uses perfect square scaling:

| Skill Differential | Weight Transfer | Direction |
|---|---|---|
| 0 | 0 | None |
| ±1 | 1 | Positive = toward upper tiers |
| ±2 | 4 | Positive = toward upper tiers |
| ±3 | 9 | Positive = toward upper tiers |
| ±4 | 16 | Positive = toward upper tiers |
| ±5 or more | 25 (cap) | Positive = toward upper tiers |

Weight moves from Miss → Glancing → Hit for negative differential. From Crippling → Critical → Good Hit for positive differential. Cap at ±5 — no additional transfer beyond 25 weight units.

### 2.9 Position Modifier

Binary — rear attack or not rear. Recalculated every swing. Rear position produces **reliability not explosiveness** — weight transfers into Solid Hit only, upper tiers unchanged.

| Position | Weight Transfer | Destination Tier | Upper Tiers |
|---|---|---|---|
| Front | 0 | None | Unchanged |
| Rear | 50 units | Solid Hit only | Unchanged |

50 weight units pulled from Miss, Glancing, and Hit redistributed into Solid Hit. Good Hit, Critical, and Crippling weights unchanged. A rear attack makes you more reliable, not more explosive.

### 2.10 Stat Contribution — Strength and Dexterity

Strength and Dexterity augment weapon skill directly rather than feeding a separate table input.

**Effective weapon skill = trained weapon skill + (relevant stat × 0.1)**

Heavy weapons draw from Strength, light weapons from Dexterity. Same mechanic, different stat label. Effective skill is never displayed to the player.

### 2.11 Level 20 Warrior Target Table

Linear progression from level 1 to level 20. Boundary positions move left per level at the rates below. Curve shape revisited through playtesting.

| Tier | Level 1 % | Level 20 % | Left Shift Per Level |
|---|---|---|---|
| Miss | 17.5% | 2% | 0.816% |
| Glancing | 40% | 13% | 2.237% from boundary |
| Hit | 30% | 20% | 2.763% from boundary |
| Solid Hit | 10% | 35% | 1.447% from boundary |
| Good Hit | 2.5% | 25% | 0.263% from boundary |
| Critical | 0% | 3% | 0.158% from boundary |
| Crippling | 0% | 2% | 0.105% — after passive unlock |

Boundary positions are cumulative percentage values. All boundaries move left as character improves. Crippling boundary movement only applies after class passive unlock. Prior to unlock those percentage points redistribute into Critical.

---

## 3. Step 2 — Avoidance Roll (DEFINED — IMPLEMENTATION PENDING)

Avoidance happens before damage is applied. The hit tier from Step 1 may be negated entirely based on defender avoidance capabilities. Avoidance and mitigation are explicitly separate systems — avoidance changes the tier outcome, mitigation reduces the hit that landed.

### 3.1 Waterfall Model

Three independent sequential checks. Each check fires only if the previous check failed. All three use **binary outcomes** — either the attack fully misses or the original hit tier from Step 1 stands unchanged. No partial avoidance.

| Order | Check | Stat Input | Success Outcome | Fail Outcome |
|---|---|---|---|---|
| 1st | Riposte | Dexterity | Miss + counter attack | Proceed to Parry check |
| 2nd | Parry | Dexterity | Miss | Proceed to Dodge check |
| 3rd | Dodge | Agility | Miss | Original hit tier stands |

### 3.2 Avoidance Outcomes

- **Dodge success** — attack resolves as Miss. No damage. No counter attack.
- **Parry success** — attack resolves as Miss. No damage. Circumstantial — parry eligibility depends on attack type. A lion bite cannot be parried. A sword swing can.
- **Riposte success** — attack resolves as Miss for the incoming attack. Additionally fires a counter attack against the attacker. Counter attack bypasses Step 2 (no avoidance check) and Step 4 (no mitigation). Deals reduced damage calculated in Step 3 only.
- **All checks fail** — original hit tier from Step 1 proceeds to Step 3 unchanged.

### 3.3 Dodge Curve — Agility Input

Dodge success probability is determined by the defender's Agility stat through a multi-segment curve. The curve is never displayed to players — only the observable outcome (Miss or hit proceeds) is visible.

| Agility Range | Dodge % | Notes |
|---|---|---|
| 1-50 | 0.1% | Flat floor — minimal but non-zero |
| 51-75 | 0.136% to 1.0% | Slow linear ramp |
| 76-100 | 1.16% to 5.0% | Steeper linear ramp — most classes live here at gear |
| 101-135 | 5.14% to 10.0% | Moderate ramp — monk territory begins |
| 136-209 | 10.07% to 14.93% | Slow ramp — high agility classes |
| 210+ | ~15% asymptote | Effective ceiling — diminishing returns |

> **Implementation note:** Full per-point values for Agility 1-250 were defined in design session and are available in conversation history. Implementation should use the full lookup table rather than this summary.

**Design intent by class:** most classes land at 60-100 Agility at full gear (2-5% dodge). Monks start around 100 Agility and scale into 150+ range (10-15% dodge). Monks are intended to tank via avoidance — the curve is designed to support this identity.

### 3.4 Parry and Riposte — Dexterity Input

Parry and Riposte use the same curve shape as Dodge but with Dexterity as the stat input. Specific curve values for Parry and Riposte are flagged as tuning parameters — the Dodge curve is locked in as the shape reference but Parry and Riposte will be differentiated through playtesting.

**Parry circumstance filter:** Parry is not eligible against all attack types. Beast attacks (bites, claws), unarmed attacks, and certain ability-driven attacks bypass the Parry check entirely and proceed directly to the Dodge check. Implementation requires attack type flagging on mob abilities and auto attacks.

### 3.5 Riposte Counter Attack

When Riposte succeeds, a counter attack fires immediately after the incoming attack resolves as Miss. Counter attack properties:

- Bypasses Step 2 — no avoidance check applied to the counter attack
- Bypasses Step 4 — no mitigation applied to the counter attack
- Damage calculated in Step 3 only — at a reduced output relative to a standard attack
- Reduced damage amount — specific reduction percentage is a tuning parameter, not yet defined

The counter attack fires automatically — it is not a player-triggered ability. Riposte success produces both the Miss outcome and the counter attack as a single resolution event.

---

## 4. Step 3 — Damage Application (PARTIALLY DEFINED)

Step 3 takes the final hit tier from Step 2 and produces a raw damage number. This number then passes to Step 4 for mitigation.

### 4.1 Tier Damage Outputs

Approximate damage outputs per tier are defined as design targets. Exact formulas and variance ranges are pending:

| Tier | Damage Output | Status | Notes |
|---|---|---|---|
| Miss | 0 | Locked | No damage |
| Glancing | ~25% of base | Approximate | Partial contact — exact percentage TBD |
| Hit | ~60% of base | Approximate | Clean contact — exact percentage TBD |
| Solid Hit | 100% of base | Reference point | Full weapon damage — this is the baseline |
| Good Hit | 100% + minor bonus | Approximate | Bonus amount TBD |
| Critical | 100% + bonus | Approximate | Bonus amount TBD |
| Crippling | 100% + major bonus | Approximate | Bonus amount TBD — class passive required |

Base damage is weapon base damage modified by Strength (heavy weapons) or Dexterity (light weapons). The specific formula connecting stat values to base damage is not yet defined.

**Variance:** damage output has meaningful spread even on identical inputs. This variance is intentional — it serves the opacity design by making per-swing reverse engineering unreliable. Variance range is a tuning parameter.

### 4.2 Open Questions — Step 3

- Exact damage percentage per tier — Glancing, Hit, Good Hit, Critical, Crippling bonus amounts
- Base damage formula — how Strength and Dexterity scale weapon base damage
- Variance range — how much spread exists on damage output per tier
- Riposte counter attack damage — specific reduction relative to standard attack
- Whether damage variance is applied before or after tier multiplier

---

## 5. Step 4 — Mitigation (NAMED — UNDEFINED)

Step 4 takes raw damage from Step 3 and applies defensive reductions to produce final damage applied to HP. Mitigation is explicitly separate from avoidance — avoidance prevents the hit, mitigation reduces the hit that landed.

**Design principles established:**

- Armor is the primary mitigation input — heavy armor reduces damage output after hit tier is resolved
- Mitigation and avoidance are independent defensive stats with distinct roles — this enables real build diversity between agility-focused and armor-focused defensive characters
- Stacking cap — avoidance and mitigation contributions are independently capped to prevent a fully solved defensive meta
- A Crippling hit through heavy armor should communicate a dangerous event without necessarily one-shotting — mitigation cannot trivialize tier outcomes
- A Glancing hit against an unarmored target should still deal meaningful damage — avoidance and mitigation together cannot create invulnerability

Everything beyond these principles is undefined. Step 4 is the least developed part of the combat pipeline and requires a dedicated design session before implementation.

### 5.1 Open Questions — Step 4

- Armor value system — how is armor expressed as a stat and how does it translate to damage reduction percentage
- Mitigation formula — flat reduction, percentage reduction, or a combination
- Buff-based absorption — Rune-style spells that intercept damage before HP is affected. How does absorption interact with the mitigation step
- Stacking cap values — what are the independent ceilings for avoidance and mitigation
- Whether mitigation scales with attacker level differential — a high level mob's damage may partially bypass mitigation
- Natural armor versus equipped armor — do races or classes have innate mitigation separate from gear

---

## 6. Implementation Notes

### 6.1 What Is Locked

- Step 1 complete — weighted table system, all modifiers, class starting tables, level 20 warrior target
- Step 2 architecture — waterfall model, three checks, binary outcomes, agility curve shape
- Step 3 tier outputs — approximate percentages as design targets
- Step 4 design principles — separation from avoidance, armor as primary input, stacking caps

### 6.2 What Requires Design Before Implementation

- Step 2 — Parry and Riposte specific curve values (playtesting will tune these)
- Step 2 — Parry circumstance filter implementation (attack type flagging system)
- Step 2 — Riposte counter attack damage reduction percentage
- Step 3 — Exact damage percentages per tier
- Step 3 — Base damage formula connecting stats to weapon damage
- Step 3 — Damage variance range
- Step 4 — Complete mitigation system

### 6.3 Recommended Implementation Order

1. **Implement Step 1 first** — weighted table with all modifiers. This is fully defined and can be validated in isolation.
2. **Implement Step 3 second** using approximate tier outputs — this allows end-to-end testing of Steps 1 and 3 together before avoidance and mitigation are added.
3. **Implement Step 2 third** — adds avoidance layer on top of working hit and damage resolution.
4. **Implement Step 4 last** — mitigation can be stubbed as zero reduction initially and filled in once the design session produces the formula.

### 6.4 Class Table Implementation Note

The class starting tables in Section 2.5 supersede any tables in the earlier `combat_math_reference.docx` document. The values here reflect the final distribution editor session outputs. If there are discrepancies between documents, use the values in this document.

---

*Eventide Combat Pipeline Reference — June 2026 — Use this document over combat_math_reference.docx where values conflict*
