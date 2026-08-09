import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { ClassService, Class } from '../class.service';
import { MobService, Mob } from '../mob.service';
import { ItemService, Item } from '../item.service';
import { ADMIN_STYLES } from '../shared/admin-styles';
import { CombatantPanel } from './combatant-panel';
import { CombatantForm, emptyCombatantForm, toCombatant } from './combatant-form';
import {
  AttackContext, FightSide, FightStats, SwingStats, TIER_COLORS, TIER_LABELS, TIER_ORDER, HitTier,
  simulateFight, simulateSwings,
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
      Client-side Monte Carlo re-implementation of the live combat pipeline (Hit Roll → Avoidance →
      Damage → Mitigation-stub). Not wired to the game — a design sandbox for tuning tier tables,
      avoidance stats, and weapon numbers before authoring them for real in Classes / Mobs / Items.
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

  readonly tierKeys: HitTier[] = [...TIER_ORDER];
  readonly tierLabels = TIER_LABELS;
  readonly tierColors = TIER_COLORS;

  ngOnInit(): void {
    this.classSvc.getAll().pipe(catchError(() => of([]))).subscribe(c => (this.classes = c));
    this.mobSvc.getAll().pipe(catchError(() => of([]))).subscribe(m => (this.mobs = m));
    this.itemSvc.getAll().pipe(catchError(() => of([]))).subscribe(i => (this.items = i));
    this.recompute();
  }

  recompute(): void {
    const a = toCombatant(this.formA);
    const b = toCombatant(this.formB);

    const ctxAtoB: AttackContext = {
      attacker: a, defender: b, isRearAttack: this.formA.isRearAttack,
      isParryable: this.formA.attackIsParryable,
      weaponBaseDamage: this.formA.weaponBaseDamage, relevantStat: this.formA.relevantStat,
    };
    const ctxBtoA: AttackContext = {
      attacker: b, defender: a, isRearAttack: this.formB.isRearAttack,
      isParryable: this.formB.attackIsParryable,
      weaponBaseDamage: this.formB.weaponBaseDamage, relevantStat: this.formB.relevantStat,
    };

    const trials = Math.max(100, Math.floor(this.swingTrials) || 0);
    this.rAtoB = simulateSwings(ctxAtoB, this.formA.weaponDelay, trials);
    this.rBtoA = simulateSwings(ctxBtoA, this.formB.weaponDelay, trials);

    const sideA: FightSide = {
      combatant: a, weaponBaseDamage: this.formA.weaponBaseDamage, weaponDelay: this.formA.weaponDelay,
      relevantStat: this.formA.relevantStat, isParryable: this.formA.attackIsParryable, maxHp: this.formA.maxHp,
    };
    const sideB: FightSide = {
      combatant: b, weaponBaseDamage: this.formB.weaponBaseDamage, weaponDelay: this.formB.weaponDelay,
      relevantStat: this.formB.relevantStat, isParryable: this.formB.attackIsParryable, maxHp: this.formB.maxHp,
    };

    const fTrials = Math.max(10, Math.floor(this.fightTrials) || 0);
    const cap = Math.max(1, this.maxFightSeconds || 60);
    this.fight = simulateFight(sideA, sideB, fTrials, cap);
  }

  swap(): void {
    [this.formA, this.formB] = [this.formB, this.formA];
    this.recompute();
  }

  resetAll(): void {
    this.formA = emptyCombatantForm('Combatant A');
    this.formB = emptyCombatantForm('Combatant B');
    this.recompute();
  }
}
