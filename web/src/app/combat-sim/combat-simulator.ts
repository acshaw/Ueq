import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { ClassService, Class } from '../class.service';
import { MobService, Mob } from '../mob.service';
import { ItemService, Item } from '../item.service';
import { ADMIN_STYLES } from '../shared/admin-styles';
import { CombatantPanel } from './combatant-panel';
import { CombatantForm, emptyCombatantForm, relevantStatForAtk, toCombatant } from './combatant-form';
import {
  AttackContext, AttackTrace, FightSide, FightStats, SwingStats, TIER_COLORS, TIER_LABELS, TIER_ORDER, HitTier,
  simulateFight, simulateSwings, traceAttack,
} from './combat-math';

/**
 * Combat design iteration tool (see combat-guide for the pipeline this mirrors). Everything here runs
 * client-side (Monte Carlo over combat-math.ts, a hand-kept TS port of CombatResolver.cs) — no server
 * round trip, so tweaking a stat and re-reading the results is instant. Two independently editable
 * combatants ("Load class/mob/weapon" prefills from the real content DB, then every field is a plain
 * number you can hand-tune); results recompute on every edit.
 */
@Component({
  selector: 'app-combat-simulator',
  imports: [CommonModule, FormsModule, CombatantPanel],
  template: `
    <div class="toolbar">
      <h1>Combat Simulator</h1>
      <div class="actions">
        <button class="small" (click)="swap()">⇄ Swap A / B</button>
        <button class="small" (click)="resetAll()">Reset</button>
      </div>
    </div>
    <p class="muted intro">
      Client-side Monte Carlo re-implementation of the live combat pipeline (Hit Roll, ATK-derived since
      5.1.5 → Avoidance → Damage → Mitigation-stub). Not wired to the game — a design sandbox for tuning
      ATK formulas, avoidance stats, and weapon numbers before authoring them for real in Classes / Mobs
      / Items. "Swing analysis" below is the wholistic Monte Carlo view (aggregate odds over many
      swings); "Swing trace" is the piece-by-piece view of one actual resolved swing.
    </p>

    <div class="panels">
      <app-combatant-panel [form]="formA" [classes]="classes" [mobs]="mobs" [items]="items" (changed)="recompute()" />
      <app-combatant-panel [form]="formB" [classes]="classes" [mobs]="mobs" [items]="items" (changed)="recompute()" />
    </div>

    <section>
      <div class="rowhead">
        <h3>Swing analysis <span class="muted">({{ swingTrials }} swings/direction)</span></h3>
        <label class="inline">Trials <input type="number" min="100" step="1000" [(ngModel)]="swingTrials" name="swingTrials" (ngModelChange)="recompute()" /></label>
      </div>

      <div class="swing-cols">
        <div class="swing-col">
          <h4>{{ formA.label || 'A' }} → {{ formB.label || 'B' }}</h4>
          @if (rAtoB) { <ng-container *ngTemplateOutlet="swingBlock; context: { $implicit: rAtoB }" /> }
        </div>
        <div class="swing-col">
          <h4>{{ formB.label || 'B' }} → {{ formA.label || 'A' }}</h4>
          @if (rBtoA) { <ng-container *ngTemplateOutlet="swingBlock; context: { $implicit: rBtoA }" /> }
        </div>
      </div>
    </section>

    <ng-template #swingBlock let-r>
      <div class="stat-row">
        <div class="stat-tile"><span class="v">{{ r.hitRate.toFixed(1) }}%</span><span class="k">landed</span></div>
        <div class="stat-tile"><span class="v">{{ r.avgDamagePerSwing.toFixed(1) }}</span><span class="k">avg dmg / swing</span></div>
        <div class="stat-tile"><span class="v">{{ r.avgDamagePerLandedHit.toFixed(1) }}</span><span class="k">avg dmg / landed hit</span></div>
        <div class="stat-tile"><span class="v">{{ r.dps.toFixed(1) }}</span><span class="k">DPS</span></div>
      </div>

      <div class="bars">
        @for (t of tierKeys; track t) {
          <div class="bar-row">
            <span class="bar-label">{{ tierLabels[t] }}</span>
            <div class="bar-track">
              <div class="bar-fill" [style.width.%]="r.tierPct[t]" [style.background]="tierColors[t]"></div>
            </div>
            <span class="bar-pct">{{ r.tierPct[t].toFixed(1) }}%</span>
          </div>
        }
      </div>

      <div class="avoid-row">
        <span>Raw miss <b>{{ r.rawMissRate.toFixed(1) }}%</b></span>
        <span>Dodge <b>{{ r.dodgeRate.toFixed(1) }}%</b></span>
        <span>Parry <b>{{ r.parryRate.toFixed(1) }}%</b></span>
        <span>Riposte <b>{{ r.riposteRate.toFixed(1) }}%</b></span>
        @if (r.riposteRate > 0) { <span>Riposte counter-dmg avg <b>{{ r.avgRiposteDamage.toFixed(1) }}</b></span> }
      </div>
    </ng-template>

    <section>
      <div class="rowhead">
        <h3>Swing trace <span class="muted">(one swing, piece by piece)</span></h3>
        <div class="fight-controls">
          <button class="small" (click)="rollTrace('atob')">Roll {{ formA.label || 'A' }} → {{ formB.label || 'B' }}</button>
          <button class="small" (click)="rollTrace('btoa')">Roll {{ formB.label || 'B' }} → {{ formA.label || 'A' }}</button>
        </div>
      </div>
      <p class="muted">Resolves ONE real swing through the exact same pipeline as the Monte Carlo runs above, but
        shows every intermediate value instead of just the outcome — how ATK shapes the base table, how each
        modifier reshapes it, the actual roll, each avoidance check, and the damage math. Re-roll to see a
        different random outcome; the aggregate odds above already account for every possibility this could land on.</p>

      <div class="trace-cols">
        <div class="trace-col">
          @if (traceAtoB) { <ng-container *ngTemplateOutlet="traceBlock; context: { $implicit: traceAtoB }" /> }
          @else { <p class="muted">Click "Roll" to trace a swing.</p> }
        </div>
        <div class="trace-col">
          @if (traceBtoA) { <ng-container *ngTemplateOutlet="traceBlock; context: { $implicit: traceBtoA }" /> }
          @else { <p class="muted">Click "Roll" to trace a swing.</p> }
        </div>
      </div>
    </section>

    <ng-template #traceBlock let-tr>
      <div class="trace-card">
        <h4>Step 1 — Hit Roll <span class="muted">(ATK {{ tr.atk.toFixed(1) }}, {{ (tr.atkFraction * 100).toFixed(0) }}% toward L20 table)</span></h4>
        @for (stage of tr.stages; track stage.label) {
          <div class="trace-stage">
            <div class="trace-stage-head"><b>{{ stage.label }}</b><span class="muted">{{ stage.note }}</span></div>
            <div class="bars compact">
              @for (t of tierKeys; track t) {
                <div class="bar-row">
                  <span class="bar-label">{{ tierLabels[t] }}</span>
                  <div class="bar-track"><div class="bar-fill" [style.width.%]="(stage.table[t] / tr.rollTotal) * 100" [style.background]="tierColors[t]"></div></div>
                  <span class="bar-pct">{{ stage.table[t].toFixed(1) }}</span>
                </div>
              }
            </div>
          </div>
        }
        <p class="roll-line">Roll <b>{{ tr.rollValue.toFixed(1) }}</b> / {{ tr.rollTotal.toFixed(1) }} total →
          landed in <b>{{ tierLabel(tr.rawTier) }}</b></p>

        <h4>Step 2 — Avoidance</h4>
        @if (tr.rawTier === 'miss') {
          <p class="roll-line muted">Already a Miss off the table roll — avoidance never checked.</p>
        } @else {
          <p class="roll-line">Riposte: chance {{ tr.riposteChance.toFixed(2) }}%, rolled {{ tr.riposteRoll.toFixed(1) }}
            → <b>{{ tr.riposted ? 'RIPOSTED' : 'no' }}</b></p>
          @if (tr.parryChecked) {
            <p class="roll-line">Parry: chance {{ tr.parryChance.toFixed(2) }}%, rolled {{ tr.parryRoll.toFixed(1) }}
              → <b>{{ tr.parried ? 'PARRIED' : 'no' }}</b></p>
          } @else if (!tr.riposted) {
            <p class="roll-line muted">Parry: skipped (attack not parryable)</p>
          }
          @if (tr.dodgeChecked) {
            <p class="roll-line">Dodge: chance {{ tr.dodgeChance.toFixed(2) }}%, rolled {{ tr.dodgeRoll.toFixed(1) }}
              → <b>{{ tr.dodged ? 'DODGED' : 'no' }}</b></p>
          }
        }
        <p class="roll-line">Final tier: <b>{{ tierLabel(tr.finalTier) }}</b></p>

        <h4>Step 3 — Damage</h4>
        @if (tr.finalTier === 'miss') {
          <p class="roll-line muted">No damage — the swing missed.</p>
        } @else {
          <p class="roll-line">{{ (tr.tierPercent * 100).toFixed(0) }}% tier × {{ tr.varianceMultiplier.toFixed(3) }}
            variance × {{ tr.baseDamageWithStat.toFixed(1) }} base (w/ stat) = {{ tr.rawDamage.toFixed(1) }} raw →
            <b>{{ tr.preMitigationDamage }}</b></p>
        }
        <h4>Step 4 — Mitigation</h4>
        @if (tr.finalTier === 'miss') {
          <p class="roll-line muted">No damage to mitigate — the swing missed.</p>
        } @else {
          <p class="roll-line">Defender AC {{ tr.defenderAc }} → {{ tr.mitigationPct.toFixed(1) }}% mitigation:
            {{ tr.preMitigationDamage }} × {{ (1 - tr.mitigationPct / 100).toFixed(3) }} =
            <b>{{ tr.damage }}</b> final</p>
        }
        @if (tr.riposted) {
          <p class="roll-line">Riposte counter-attack: <b>{{ tr.riposteDamage }}</b> damage back to the attacker.</p>
        }
      </div>
    </ng-template>

    <section>
      <div class="rowhead">
        <h3>Fight to the death</h3>
        <div class="fight-controls">
          <label class="inline">Trials <input type="number" min="10" step="100" [(ngModel)]="fightTrials" name="fightTrials" (ngModelChange)="recompute()" /></label>
          <label class="inline">Cap (s) <input type="number" min="1" step="10" [(ngModel)]="maxFightSeconds" name="maxFightSeconds" (ngModelChange)="recompute()" /></label>
        </div>
      </div>
      <p class="muted">Each side swings on its own weapon delay (no rear-attack bonus — a straight-up duel) until one side's HP hits zero, or the time cap is reached (a stalemate — widen the gap between the two sides or raise the cap).</p>

      @if (fight) {
        <div class="stat-row">
          <div class="stat-tile a"><span class="v">{{ fight.aWinRate.toFixed(1) }}%</span><span class="k">{{ formA.label || 'A' }} wins</span></div>
          <div class="stat-tile b"><span class="v">{{ fight.bWinRate.toFixed(1) }}%</span><span class="k">{{ formB.label || 'B' }} wins</span></div>
          <div class="stat-tile"><span class="v">{{ fight.timeoutRate.toFixed(1) }}%</span><span class="k">stalemate</span></div>
        </div>
        <div class="stat-row">
          <div class="stat-tile"><span class="v">{{ fight.avgTtk.toFixed(1) }}s</span><span class="k">avg time-to-kill</span></div>
          <div class="stat-tile"><span class="v">{{ fight.medianTtk.toFixed(1) }}s</span><span class="k">median TTK</span></div>
          <div class="stat-tile"><span class="v">{{ fight.minTtk.toFixed(1) }}–{{ fight.maxTtk.toFixed(1) }}s</span><span class="k">min–max TTK</span></div>
          <div class="stat-tile"><span class="v">{{ fight.avgSurvivorHpPct.toFixed(0) }}%</span><span class="k">winner's avg HP left</span></div>
        </div>
      }
    </section>
  `,
  styles: [
    ADMIN_STYLES,
    `
    .intro { margin-top: -0.4rem; margin-bottom: 1rem; }
    .panels { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; margin-bottom: 0.75rem; }
    .inline { display: inline-flex; align-items: center; gap: 0.4rem; font-size: 0.82rem; color: #555; }
    .inline input { width: 6rem; padding: 0.2rem 0.35rem; }
    .fight-controls { display: flex; gap: 1rem; }

    .swing-cols { display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem; }
    .swing-cols h4 { margin: 0 0 0.5rem; font-size: 0.9rem; }

    .stat-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 0.5rem; margin-bottom: 0.6rem; }
    .stat-tile { border: 1px solid #eee; border-radius: 6px; padding: 0.5rem 0.6rem; display: flex;
                 flex-direction: column; gap: 0.15rem; }
    .stat-tile .v { font-size: 1.1rem; font-weight: 700; }
    .stat-tile .k { font-size: 0.72rem; color: #888; }
    .stat-tile.a .v { color: #673ab7; }
    .stat-tile.b .v { color: #00897b; }

    .bars { display: flex; flex-direction: column; gap: 0.3rem; margin-bottom: 0.5rem; }
    .bar-row { display: grid; grid-template-columns: 5.5rem 1fr 3rem; align-items: center; gap: 0.5rem; }
    .bar-label { font-size: 0.78rem; color: #555; }
    .bar-track { background: #f2f2f2; border-radius: 3px; height: 0.85rem; overflow: hidden; }
    .bar-fill { height: 100%; }
    .bar-pct { font-size: 0.78rem; color: #666; text-align: right; }

    .avoid-row { display: flex; flex-wrap: wrap; gap: 0.9rem; font-size: 0.78rem; color: #666; margin-top: 0.3rem; }
    .avoid-row b { color: #333; }

    .trace-cols { display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem; }
    .trace-card { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem; }
    .trace-card h4 { margin: 0.7rem 0 0.3rem; font-size: 0.82rem; color: #444; }
    .trace-card h4:first-child { margin-top: 0; }
    .trace-stage { margin-bottom: 0.5rem; }
    .trace-stage-head { display: flex; justify-content: space-between; gap: 0.5rem; font-size: 0.78rem; margin-bottom: 0.2rem; }
    .trace-stage-head .muted { text-align: right; }
    .bars.compact .bar-row { grid-template-columns: 4.5rem 1fr 2.5rem; }
    .bars.compact .bar-track { height: 0.6rem; }
    .roll-line { font-size: 0.8rem; color: #444; margin: 0.2rem 0; }
    .roll-line b { color: #111; }
    `,
  ],
})
export class CombatSimulator implements OnInit {
  private readonly classSvc = inject(ClassService);
  private readonly mobSvc = inject(MobService);
  private readonly itemSvc = inject(ItemService);

  classes: Class[] = [];
  mobs: Mob[] = [];
  items: Item[] = [];

  formA: CombatantForm = emptyCombatantForm('Combatant A');
  formB: CombatantForm = emptyCombatantForm('Combatant B');

  swingTrials = 20000;
  fightTrials = 1000;
  maxFightSeconds = 60;

  rAtoB: SwingStats | null = null;
  rBtoA: SwingStats | null = null;
  fight: FightStats | null = null;
  traceAtoB: AttackTrace | null = null;
  traceBtoA: AttackTrace | null = null;

  readonly tierKeys: HitTier[] = [...TIER_ORDER];
  readonly tierLabels = TIER_LABELS;
  readonly tierColors = TIER_COLORS;

  ngOnInit(): void {
    this.classSvc.getAll().pipe(catchError(() => of([]))).subscribe(c => (this.classes = c));
    this.mobSvc.getAll().pipe(catchError(() => of([]))).subscribe(m => (this.mobs = m));
    this.itemSvc.getAll().pipe(catchError(() => of([]))).subscribe(i => (this.items = i));
    this.recompute();
  }

  private buildContexts(): { a: ReturnType<typeof toCombatant>; b: ReturnType<typeof toCombatant>; ctxAtoB: AttackContext; ctxBtoA: AttackContext } {
    const a = toCombatant(this.formA);
    const b = toCombatant(this.formB);

    const ctxAtoB: AttackContext = {
      attacker: a, defender: b, isRearAttack: this.formA.isRearAttack,
      isParryable: this.formA.attackIsParryable,
      weaponBaseDamage: this.formA.weaponBaseDamage, weaponBonusDamage: this.formA.weaponBonusDamage,
      relevantStat: relevantStatForAtk(this.formA),
    };
    const ctxBtoA: AttackContext = {
      attacker: b, defender: a, isRearAttack: this.formB.isRearAttack,
      isParryable: this.formB.attackIsParryable,
      weaponBaseDamage: this.formB.weaponBaseDamage, weaponBonusDamage: this.formB.weaponBonusDamage,
      relevantStat: relevantStatForAtk(this.formB),
    };
    return { a, b, ctxAtoB, ctxBtoA };
  }

  recompute(): void {
    const { a, b, ctxAtoB, ctxBtoA } = this.buildContexts();

    const trials = Math.max(100, Math.floor(this.swingTrials) || 0);
    this.rAtoB = simulateSwings(ctxAtoB, this.formA.weaponDelay, trials);
    this.rBtoA = simulateSwings(ctxBtoA, this.formB.weaponDelay, trials);

    const sideA: FightSide = {
      combatant: a, weaponBaseDamage: this.formA.weaponBaseDamage, weaponBonusDamage: this.formA.weaponBonusDamage,
      weaponDelay: this.formA.weaponDelay,
      relevantStat: relevantStatForAtk(this.formA), isParryable: this.formA.attackIsParryable, maxHp: this.formA.maxHp,
    };
    const sideB: FightSide = {
      combatant: b, weaponBaseDamage: this.formB.weaponBaseDamage, weaponBonusDamage: this.formB.weaponBonusDamage,
      weaponDelay: this.formB.weaponDelay,
      relevantStat: relevantStatForAtk(this.formB), isParryable: this.formB.attackIsParryable, maxHp: this.formB.maxHp,
    };

    const fTrials = Math.max(10, Math.floor(this.fightTrials) || 0);
    const cap = Math.max(1, this.maxFightSeconds || 60);
    this.fight = simulateFight(sideA, sideB, fTrials, cap);
  }

  tierLabel(t: HitTier): string {
    return this.tierLabels[t];
  }

  rollTrace(direction: 'atob' | 'btoa'): void {
    const { ctxAtoB, ctxBtoA } = this.buildContexts();
    if (direction === 'atob') this.traceAtoB = traceAttack(ctxAtoB);
    else this.traceBtoA = traceAttack(ctxBtoA);
  }

  swap(): void {
    [this.formA, this.formB] = [this.formB, this.formA];
    this.traceAtoB = null;
    this.traceBtoA = null;
    this.recompute();
  }

  resetAll(): void {
    this.formA = emptyCombatantForm('Combatant A');
    this.formB = emptyCombatantForm('Combatant B');
    this.traceAtoB = null;
    this.traceBtoA = null;
    this.recompute();
  }
}
