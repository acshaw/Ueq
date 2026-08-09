import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Class } from '../class.service';
import { Mob } from '../mob.service';
import { Item } from '../item.service';
import { TIER_LABELS, TIER_ORDER, HitTier, tierTotal } from './combat-math';
import { CombatantForm, loadFromClass, loadFromMob, loadFromWeapon, recomputeRelevantStat } from './combatant-form';

/**
 * One side of the combat simulator (5.1's `Combatant` + weapon fields, editable). "Load class/mob/
 * weapon" prefills from live content-DB data (via the services the other editors already use); every
 * field stays freely editable afterward. Emits `changed` on any edit so the parent recomputes.
 */
@Component({
  selector: 'app-combatant-panel',
  imports: [FormsModule],
  template: `
    <div class="panel">
      <input class="label-input" [(ngModel)]="form.label" name="label" (ngModelChange)="emit()" />

      <div class="loaders">
        <label>Load class…
          <select [(ngModel)]="pickClass" name="pickClass" (ngModelChange)="onClass($event)">
            <option value="">—</option>
            @for (c of classes; track c.classId) { <option [value]="c.classId">{{ c.className }}</option> }
          </select>
        </label>
        <label>Load mob…
          <select [(ngModel)]="pickMob" name="pickMob" (ngModelChange)="onMob($event)">
            <option value="">—</option>
            @for (m of mobs; track m.mobId) { <option [value]="m.mobId">{{ m.displayName }} (L{{ m.mobLevel }})</option> }
          </select>
        </label>
        <label>Load weapon…
          <select [(ngModel)]="pickWeapon" name="pickWeapon" (ngModelChange)="onWeapon($event)">
            <option value="">—</option>
            @for (i of items; track i.itemId) { <option [value]="i.itemId">{{ i.displayName }}</option> }
          </select>
        </label>
      </div>

      <div class="grid">
        <label>Level <input type="number" min="1" [(ngModel)]="form.level" name="level" (ngModelChange)="emit()" /></label>
        <label>Weapon category
          <select [(ngModel)]="form.weaponCategory" name="weaponCategory" (ngModelChange)="onCategoryChange()">
            <option [ngValue]="0">Might (STR)</option>
            <option [ngValue]="1">Finesse (DEX)</option>
          </select>
        </label>
        <label>Weapon base damage <input type="number" [(ngModel)]="form.weaponBaseDamage" name="weaponBaseDamage" (ngModelChange)="emit()" /></label>
        <label>Weapon delay (s) <input type="number" step="0.1" min="0.1" [(ngModel)]="form.weaponDelay" name="weaponDelay" (ngModelChange)="emit()" /></label>
        <label>Relevant stat ({{ form.weaponCategory === 0 ? 'STR' : 'DEX' }})
          <input type="number" [(ngModel)]="form.relevantStat" name="relevantStat" (ngModelChange)="emit()" />
        </label>
        <label>Weapon skill <input type="number" [(ngModel)]="form.weaponSkill" name="weaponSkill" (ngModelChange)="emit()" /></label>
        <label>Avoidance Agility <input type="number" [(ngModel)]="form.avoidanceAgility" name="avoidanceAgility" (ngModelChange)="emit()" /></label>
        <label>Avoidance Dexterity <input type="number" [(ngModel)]="form.avoidanceDexterity" name="avoidanceDexterity" (ngModelChange)="emit()" /></label>
        <label>Max HP <input type="number" [(ngModel)]="form.maxHp" name="maxHp" (ngModelChange)="emit()" /></label>
      </div>

      <label class="check"><input type="checkbox" [(ngModel)]="form.attackIsParryable" name="attackIsParryable" (ngModelChange)="emit()" /> Attack is parryable</label>
      <label class="check"><input type="checkbox" [(ngModel)]="form.isRearAttack" name="isRearAttack" (ngModelChange)="emit()" /> Attacking from behind (swing analyzer only)</label>

      <h4>Hit-tier weights <span class="muted">(raw units, total {{ tierTotalDisplay() }})</span></h4>
      <div class="tier-grid">
        @for (t of tierKeys; track t) {
          <label>{{ tierLabels[t] }} <input type="number" step="0.1" min="0" [(ngModel)]="form.tier[t]" name="tier-{{t}}" (ngModelChange)="emit()" /></label>
        }
      </div>
    </div>
  `,
  styles: [`
    .panel { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; }
    .label-input { font-size: 1rem; font-weight: 600; border: none; border-bottom: 1px solid #ddd;
                    padding: 0.2rem 0; margin-bottom: 0.6rem; width: 100%; }
    .loaders { display: grid; grid-template-columns: 1fr; gap: 0.35rem; margin-bottom: 0.6rem; }
    .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0 0.75rem; }
    label { display: block; margin: 0.3rem 0; font-size: 0.82rem; color: #555; }
    label.check { display: flex; gap: 0.4rem; align-items: center; margin: 0.4rem 0; }
    label.check input { width: auto; }
    input, select { width: 100%; padding: 0.3rem; box-sizing: border-box; font: inherit; font-size: 0.85rem; }
    h4 { margin: 0.75rem 0 0.35rem; font-size: 0.82rem; color: #444; }
    .tier-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0 0.75rem; }
    .muted { color: #999; font-weight: 400; }
  `],
})
export class CombatantPanel {
  @Input({ required: true }) form!: CombatantForm;
  @Input() classes: Class[] = [];
  @Input() mobs: Mob[] = [];
  @Input() items: Item[] = [];
  @Output() changed = new EventEmitter<void>();

  readonly tierKeys: HitTier[] = [...TIER_ORDER];
  readonly tierLabels = TIER_LABELS;

  pickClass = '';
  pickMob = '';
  pickWeapon = '';

  emit(): void { this.changed.emit(); }

  tierTotalDisplay(): string {
    return tierTotal(this.form.tier).toFixed(1);
  }

  onClass(id: string): void {
    const cls = this.classes.find(c => c.classId === id);
    if (cls) loadFromClass(this.form, cls);
    this.emit();
  }

  onMob(id: string): void {
    const mob = this.mobs.find(m => m.mobId === id);
    if (mob) loadFromMob(this.form, mob);
    this.emit();
  }

  onWeapon(id: string): void {
    const item = this.items.find(i => i.itemId === id);
    if (item) loadFromWeapon(this.form, item);
    this.emit();
  }

  onCategoryChange(): void {
    recomputeRelevantStat(this.form);
    this.emit();
  }
}
