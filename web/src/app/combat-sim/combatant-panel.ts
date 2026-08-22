import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Class } from '../class.service';
import { Mob } from '../mob.service';
import { Item } from '../item.service';
import { TIER_LABELS, TIER_ORDER, HitTier, avoidanceChance, defenseCap, mitigationChance, offenseCap, weaponSkillCap } from './combat-math';
import { CombatantForm, atk, clampAvoidanceSkill, clampDefense, clampOffense, clampWeaponSkill, dodgeValue, loadFromClass, loadFromMob, loadFromWeapon, parryValue, resolvedTable, riposteValue } from './combatant-form';

/**
 * One side of the combat simulator (5.1's `Combatant` + weapon fields, editable). "Load class/mob/
 * weapon" prefills from live content-DB data (via the services the other editors already use); every
 * field stays freely editable afterward. Emits `changed` on any edit so the parent recomputes.
 *
 * Layout (2026-08-13): raw stats (STR/STA/AGI/DEX/INT/WIS/CHA), the ATK readout, and the resulting
 * hit-tier table run down the left column; weapon/level fields, checkboxes, and the Avoidance section
 * run down the right column — single-column lists rather than a 2-up grid.
 *
 * 5.1.5, revised repeatedly (2026-08-13): weaponSkill and offense are real editable fields (mirroring
 * the game's trainable PlayerWeaponSkills/PlayerOffense), clamped to that level's cap on every edit
 * (onWeaponSkillChange/onOffenseChange → combatant-form.ts's clampWeaponSkill/clampOffense) rather than
 * a read-only "assume fully trained" formula — "Load class" still defaults both to their level's cap,
 * but either can be hand-lowered to see how an undertrained character performs. A mob-loaded combatant
 * shows its authored ATK instead (`form.manualAtk`, still editable — mobs are the one case where ATK
 * genuinely is a hand-authored number, AD3), with weaponSkill/offense unused.
 *
 * Avoidance rework (2026-08-13): same treatment — Defense/Dodge/Parry/Riposte are real editable fields
 * for a class-based combatant (clamped on every edit), or three directly-authored numbers
 * (`manualDodge`/`manualParry`/`manualRiposte`) for a mob, mirroring ATK's `manualAtk` branch exactly.
 * A shared readout line shows the resulting Dodge/Parry/Riposte % regardless of which branch is active.
 */
@Component({
  selector: 'app-combatant-panel',
  imports: [FormsModule],
  template: `
    <div class="panel">
      <input class="label-input" [(ngModel)]="form.label" name="label" (ngModelChange)="emit()" />

      <div class="loaders">
        <label>Load…
          <select [(ngModel)]="pickCombatant" name="pickCombatant" (ngModelChange)="onCombatant($event)">
            <option value="">—</option>
            <optgroup label="Classes">
              @for (c of classes; track c.classId) { <option [value]="'class:' + c.classId">{{ c.className }}</option> }
            </optgroup>
            <optgroup label="Mobs">
              @for (m of mobs; track m.mobId) { <option [value]="'mob:' + m.mobId">{{ m.displayName }} (L{{ m.mobLevel }})</option> }
            </optgroup>
          </select>
        </label>
        <label>Load weapon…
          <select [(ngModel)]="pickWeapon" name="pickWeapon" (ngModelChange)="onWeapon($event)">
            <option value="">—</option>
            @for (i of weaponItems(); track i.itemId) { <option [value]="i.itemId">{{ i.displayName }}</option> }
          </select>
        </label>
      </div>

      <div class="body">
        <div class="col">
          <label>STR <input type="number" [(ngModel)]="form.str" name="str" (ngModelChange)="emit()" /></label>
          <label>STA <input type="number" [(ngModel)]="form.sta" name="sta" (ngModelChange)="emit()" /></label>
          <label>AGI <input type="number" [(ngModel)]="form.agi" name="agi" (ngModelChange)="emit()" /></label>
          <label>DEX <input type="number" [(ngModel)]="form.dex" name="dex" (ngModelChange)="emit()" /></label>
          <label>INT <input type="number" [(ngModel)]="form.int" name="int" (ngModelChange)="emit()" /></label>
          <label>WIS <input type="number" [(ngModel)]="form.wis" name="wis" (ngModelChange)="emit()" /></label>
          <label>CHA <input type="number" [(ngModel)]="form.cha" name="cha" (ngModelChange)="emit()" /></label>

          @if (form.manualAtk != null) {
            <h4>ATK</h4>
            <label>ATK <span class="hint">(authored directly on this mob, AD3)</span>
              <input type="number" step="0.1" [(ngModel)]="form.manualAtk" name="manualAtk" (ngModelChange)="emit()" />
            </label>
          } @else {
            <h4>ATK: {{ atkValue().toFixed(1) }}</h4>
          }
          <div class="col readout">
            @for (t of tierKeys; track t) {
              <label>{{ tierLabels[t] }} <span class="ro">{{ resolvedTierWeight(t) }}</span></label>
            }
          </div>
        </div>

        <div class="col">
          <label>Level <input type="number" min="1" [(ngModel)]="form.level" name="level" (ngModelChange)="emit()" /></label>
          <p class="weapon-line atk-readout">
            <span>{{ form.weaponCategory === 0 ? 'STR' : 'DEX' }}</span>
            <span>DMG {{ form.weaponBaseDamage }}</span>
            <span>BON {{ form.weaponBonusDamage }}</span>
            <span>DLY {{ form.weaponDelay }}</span>
          </p>
          <p class="dps">Raw weapon ratio: <b>{{ weaponRatio() }}</b></p>
          <label>Max HP <input type="number" [(ngModel)]="form.maxHp" name="maxHp" (ngModelChange)="emit()" /></label>
          <label class="check"><input type="checkbox" [(ngModel)]="form.attackIsParryable" name="attackIsParryable" (ngModelChange)="emit()" /> Attack is parryable</label>
          <label class="check"><input type="checkbox" [(ngModel)]="form.isRearAttack" name="isRearAttack" (ngModelChange)="emit()" /> Attacking from behind (swing analyzer only)</label>

          <label>Weapon skill
            <input type="number" min="0" [max]="weaponSkillCapValue()" [(ngModel)]="form.weaponSkill" name="weaponSkill" (ngModelChange)="onWeaponSkillChange()" />
          </label>
          <label>Offense
            <input type="number" min="0" [max]="offenseCapValue()" [(ngModel)]="form.offense" name="offense" (ngModelChange)="onOffenseChange()" />
          </label>

          <h4>Avoidance</h4>
          @if (form.manualDodge != null) {
            <label>Dodge <span class="hint">(authored directly, AV3)</span>
              <input type="number" step="0.1" [(ngModel)]="form.manualDodge" name="manualDodge" (ngModelChange)="emit()" />
            </label>
            <label>Parry
              <input type="number" step="0.1" [(ngModel)]="form.manualParry" name="manualParry" (ngModelChange)="emit()" />
            </label>
            <label>Riposte
              <input type="number" step="0.1" [(ngModel)]="form.manualRiposte" name="manualRiposte" (ngModelChange)="emit()" />
            </label>
          } @else {
            <label>Defense
              <input type="number" min="0" [max]="defenseCapValue()" [(ngModel)]="form.defense" name="defense" (ngModelChange)="onDefenseChange()" />
            </label>
            <label>Dodge skill
              <input type="number" min="0" [max]="avoidanceSkillCapValue()" [(ngModel)]="form.dodgeSkill" name="dodgeSkill" (ngModelChange)="onDodgeSkillChange()" />
            </label>
            <label>Parry skill
              <input type="number" min="0" [max]="avoidanceSkillCapValue()" [(ngModel)]="form.parrySkill" name="parrySkill" (ngModelChange)="onParrySkillChange()" />
            </label>
            <label>Riposte skill
              <input type="number" min="0" [max]="avoidanceSkillCapValue()" [(ngModel)]="form.riposteSkill" name="riposteSkill" (ngModelChange)="onRiposteSkillChange()" />
            </label>
          }
          <p class="weapon-line atk-readout">
            <span>Dodge {{ dodgeChancePct().toFixed(1) }}%</span>
            <span>Parry {{ parryChancePct().toFixed(1) }}%</span>
            <span>Riposte {{ riposteChancePct().toFixed(1) }}%</span>
          </p>

          <h4>Mitigation</h4>
          <label>AC <input type="number" [(ngModel)]="form.ac" name="ac" (ngModelChange)="emit()" /></label>
          <p class="weapon-line atk-readout">
            <span>Mitigation {{ mitigationPct().toFixed(1) }}%</span>
          </p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .panel { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; }
    .label-input { font-size: 1rem; font-weight: 600; border: none; border-bottom: 1px solid #ddd;
                    padding: 0.2rem 0; margin-bottom: 0.6rem; width: 100%; }
    .loaders { display: grid; grid-template-columns: 1fr; gap: 0.35rem; margin-bottom: 0.6rem; }
    .body { display: flex; gap: 1.25rem; align-items: flex-start; }
    .col { width: 12rem; flex-shrink: 0; }
    .col label { display: flex; flex-direction: column; gap: 0.1rem; margin: 0.3rem 0; font-size: 0.82rem; color: #555; }
    .col.readout label { flex-direction: row; justify-content: space-between; align-items: center; }
    .col.readout .ro { font-weight: 600; color: #333; }
    label.check { display: flex !important; flex-direction: row !important; gap: 0.4rem; align-items: center; margin: 0.4rem 0; }
    label.check input { width: auto; }
    input, select { width: 100%; padding: 0.3rem; box-sizing: border-box; font: inherit; font-size: 0.85rem; }
    h4 { margin: 0.75rem 0 0.35rem; font-size: 0.82rem; color: #444; }
    h4:first-child { margin-top: 0; }
    .hint { font-weight: 400; color: #999; font-size: 0.72rem; }
    .muted { color: #999; font-weight: 400; }
    .dps, .atk-readout { font-size: 0.76rem; color: #777; margin: 0.4rem 0; }
    .dps b, .atk-readout b { color: #333; }
    .weapon-line { display: flex; }
    .weapon-line span { flex: 1; text-align: center; }
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

  pickCombatant = '';
  pickWeapon = '';

  emit(): void { this.changed.emit(); }

  weaponRatio(): string {
    return this.form.weaponDelay > 0 ? (this.form.weaponBaseDamage / this.form.weaponDelay).toFixed(1) : '—';
  }

  atkValue(): number {
    return atk(this.form);
  }

  weaponSkillCapValue(): number {
    return weaponSkillCap(this.form.level);
  }

  offenseCapValue(): number {
    return offenseCap(this.form.level);
  }

  onWeaponSkillChange(): void {
    this.form.weaponSkill = clampWeaponSkill(this.form.weaponSkill, this.form.level);
    this.emit();
  }

  onOffenseChange(): void {
    this.form.offense = clampOffense(this.form.offense, this.form.level);
    this.emit();
  }

  resolvedTierWeight(t: HitTier): string {
    return resolvedTable(this.form)[t].toFixed(1);
  }

  defenseCapValue(): number {
    return defenseCap(this.form.level);
  }

  // Dodge/Parry/Riposte skills share WeaponSkill's cap shape (level×5+5) — see combat-math.ts.
  avoidanceSkillCapValue(): number {
    return weaponSkillCap(this.form.level);
  }

  onDefenseChange(): void {
    this.form.defense = clampDefense(this.form.defense, this.form.level);
    this.emit();
  }

  onDodgeSkillChange(): void {
    this.form.dodgeSkill = clampAvoidanceSkill(this.form.dodgeSkill, this.form.level);
    this.emit();
  }

  onParrySkillChange(): void {
    this.form.parrySkill = clampAvoidanceSkill(this.form.parrySkill, this.form.level);
    this.emit();
  }

  onRiposteSkillChange(): void {
    this.form.riposteSkill = clampAvoidanceSkill(this.form.riposteSkill, this.form.level);
    this.emit();
  }

  dodgeChancePct(): number {
    return avoidanceChance(dodgeValue(this.form));
  }

  parryChancePct(): number {
    return avoidanceChance(parryValue(this.form));
  }

  riposteChancePct(): number {
    return avoidanceChance(riposteValue(this.form));
  }

  mitigationPct(): number {
    return mitigationChance(this.form.ac);
  }

  /** EquipSlot.Weapon (ItemDefinition.cs) — "Load weapon…" should only offer actual weapons, not
   * every item (non-equippable items default to equipSlot 11 too, so isEquippable is load-bearing here). */
  private static readonly WEAPON_SLOT = 11;

  weaponItems(): Item[] {
    return this.items.filter(i => i.isEquippable && i.equipSlot === CombatantPanel.WEAPON_SLOT);
  }

  onCombatant(value: string): void {
    const [kind, id] = value.split(':');
    if (kind === 'class') {
      const cls = this.classes.find(c => c.classId === id);
      if (cls) loadFromClass(this.form, cls);
    } else if (kind === 'mob') {
      const mob = this.mobs.find(m => m.mobId === id);
      if (mob) loadFromMob(this.form, mob);
    }
    this.emit();
  }

  onWeapon(id: string): void {
    const item = this.items.find(i => i.itemId === id);
    if (item) loadFromWeapon(this.form, item);
    this.emit();
  }
}
