import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Class, ClassService, emptyClass, CLASS_GRID_COLUMNS, CLASS_SEARCH_FIELDS, MANA_STAT_TYPES } from './class.service';
import { AbilityService } from './ability.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Class Editor (M2.10). Mirrors the retired Unity Race & Class Editor's section order:
 * Identity → XP → Base Stats → HP Formula → Mana Formula → Starting Abilities (ordered ability-id rows,
 * same shape as the Ability Editor's tag rows). ATK's Offense and Avoidance's Defense are no longer
 * per-class authored (2026-08-11 / 2026-08-13) — both are trained per-character stats now
 * (PlayerOffense.cs / PlayerAvoidanceSkills.cs), so there's no Offense/Defense section here anymore.
 * Weapon-prop cosmetic fields do NOT appear here either (RC4 — they're pure Unity-asset wiring on
 * CharacterRoster, authored in-editor).
 */
@Component({
  selector: 'app-class-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Classes</h1>
      <button class="primary" (click)="newClass()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="classes()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New class' : (model?.classId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.classId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <label>class_id <input [(ngModel)]="model.classId" name="classId" [disabled]="!isNew" placeholder="Wizard" /></label>
          <label>Display name <input [(ngModel)]="model.className" name="className" /></label>
        </section>

        <section>
          <h3>XP</h3>
          <label>XP modifier <input type="number" step="0.01" [(ngModel)]="model.xpModifier" name="xpModifier" /></label>
        </section>

        <section>
          <h3>Base stats</h3>
          <label>STR <input type="number" [(ngModel)]="model.baseStr" name="baseStr" /></label>
          <label>STA <input type="number" [(ngModel)]="model.baseSta" name="baseSta" /></label>
          <label>AGI <input type="number" [(ngModel)]="model.baseAgi" name="baseAgi" /></label>
          <label>DEX <input type="number" [(ngModel)]="model.baseDex" name="baseDex" /></label>
          <label>INT <input type="number" [(ngModel)]="model.baseInt" name="baseInt" /></label>
          <label>WIS <input type="number" [(ngModel)]="model.baseWis" name="baseWis" /></label>
          <label>CHA <input type="number" [(ngModel)]="model.baseCha" name="baseCha" /></label>
        </section>

        <section>
          <h3>HP formula</h3>
          <label>Base HP <input type="number" [(ngModel)]="model.classBaseHP" name="classBaseHP" /></label>
          <label>HP per level <input type="number" [(ngModel)]="model.hpPerLevel" name="hpPerLevel" /></label>
          <label>STA cap <input type="number" [(ngModel)]="model.staCap" name="staCap" /></label>
          <label>Base STA ratio <input type="number" step="0.01" [(ngModel)]="model.baseStaRatio" name="baseStaRatio" /></label>
          <label>STA growth rate <input type="number" step="0.01" [(ngModel)]="model.staGrowthRate" name="staGrowthRate" /></label>
        </section>

        <section>
          <h3>Mana formula</h3>
          <label>Mana stat
            <select [(ngModel)]="model.manaStatType" name="manaStatType">
              @for (name of manaStatTypes; track $index; let i = $index) { <option [ngValue]="i">{{ name }}</option> }
            </select>
          </label>
          <label>Base mana <input type="number" [(ngModel)]="model.classBaseMana" name="classBaseMana" /></label>
          <label>Mana per level <input type="number" [(ngModel)]="model.manaPerLevel" name="manaPerLevel" /></label>
          <label>Mana cap <input type="number" [(ngModel)]="model.manaCap" name="manaCap" /></label>
          <label>Base mana ratio <input type="number" step="0.01" [(ngModel)]="model.baseManaRatio" name="baseManaRatio" /></label>
          <label>Mana growth rate <input type="number" step="0.01" [(ngModel)]="model.manaGrowthRate" name="manaGrowthRate" /></label>
        </section>

        <section>
          <div class="rowhead"><h3>Starting abilities</h3><button (click)="addAbility()">+ Ability</button></div>
          @for (id of model.startingAbilityIds; track $index; let i = $index) {
            <div class="row">
              <select [(ngModel)]="model.startingAbilityIds[i]" [name]="'sa'+i">
                <option [ngValue]="''">(choose ability)</option>
                @for (aid of abilityIds(); track aid) { <option [ngValue]="aid">{{ aid }}</option> }
              </select>
              <button class="small danger" (click)="model.startingAbilityIds.splice(i,1)">✕</button>
            </div>
          } @empty { <p class="muted">No starting abilities — the hotbar will be empty at creation.</p> }
        </section>
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .row { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .row select { flex: 1; }
  `],
})
export class ClassEditor implements OnInit {
  private readonly api = inject(ClassService);
  private readonly abilityApi = inject(AbilityService);

  readonly classes = signal<Class[]>([]);
  readonly abilityIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: Class | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = CLASS_GRID_COLUMNS;
  readonly searchFields = CLASS_SEARCH_FIELDS;
  readonly manaStatTypes = MANA_STAT_TYPES;

  ngOnInit(): void {
    this.reload();
    this.abilityApi.getAll().subscribe({ next: rows => this.abilityIds.set(rows.map(r => r.abilityId)) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.classes.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newClass(): void { this.model = emptyClass(); this.isNew = true; this.modalOpen = true; }

  select(c: Class): void {
    this.model = { ...c, startingAbilityIds: [...c.startingAbilityIds] };
    this.isNew = false;
    this.modalOpen = true;
  }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

  addAbility(): void { this.model?.startingAbilityIds.push(''); }

  save(): void {
    if (!this.model) return;
    const call = this.isNew ? this.api.create(this.model) : this.api.update(this.model);
    call.subscribe({
      next: saved => { this.isNew = false; this.model = saved; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model || this.isNew) return;
    this.api.delete(this.model.classId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A class with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
