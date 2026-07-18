import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Ability, AbilityService, emptyAbility, ABILITY_GRID_COLUMNS, ABILITY_SEARCH_FIELDS, SCALING_STATS } from './ability.service';
import { AbilityTagService } from './ability-tag.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Ability Editor (M2.9, built on the 2.1.1 admin framework from day one). An ability is a
 * header (identity/targeting/resource/animation) plus three ordered lists: semantic tags, cooldown
 * links (empty = uses the shared GCD), and effects (applied in order on cast — effect_type + a shared
 * amount/scaling-stat/scaling-factor shape covers today's damage/heal effects, AB1).
 */
@Component({
  selector: 'app-ability-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Abilities</h1>
      <button class="primary" (click)="newAbility()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="abilities()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New ability' : (model?.abilityId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.abilityId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <label>ability_id <input [(ngModel)]="model.abilityId" name="abilityId" [disabled]="!isNew" placeholder="fire_bolt" /></label>
          <label>Display name <input [(ngModel)]="model.displayName" name="displayName" /></label>
          <label>Description <textarea [(ngModel)]="model.description" name="description" rows="2"></textarea></label>
        </section>

        <section>
          <h3>Targeting</h3>
          <label>Targeting
            <select [(ngModel)]="model.targetingType" name="targetingType">
              <option [ngValue]="0">Self</option>
              <option [ngValue]="1">Single Target</option>
            </select>
          </label>
          <label>Range <input type="number" [(ngModel)]="model.range" name="range" /></label>
          <label>Cast time (0=instant) <input type="number" [(ngModel)]="model.castTime" name="castTime" /></label>
        </section>

        <section>
          <h3>Resource</h3>
          <label>Mana cost (0=free) <input type="number" [(ngModel)]="model.manaCost" name="manaCost" /></label>
        </section>

        <section>
          <h3>Animation</h3>
          <label>Anim trigger <input [(ngModel)]="model.animTrigger" name="animTrigger" placeholder="Cast" /></label>
        </section>

        <section>
          <div class="rowhead"><h3>Tags</h3><button (click)="addTag()">+ Tag</button></div>
          @for (t of model.tagIds; track $index; let i = $index) {
            <div class="row">
              <select [(ngModel)]="model.tagIds[i]" [name]="'tag'+i">
                <option [ngValue]="''">(choose tag)</option>
                @for (id of tagIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
              <button class="small danger" (click)="model.tagIds.splice(i,1)">✕</button>
            </div>
          } @empty { <p class="muted">No tags.</p> }
        </section>

        <section>
          <div class="rowhead"><h3>Cooldown links</h3><button (click)="addLink()">+ Link</button></div>
          <p class="muted">Empty = uses the shared global cooldown instead of a dedicated timer.</p>
          @for (l of model.cooldownLinks; track $index; let i = $index) {
            <div class="row">
              <select [(ngModel)]="l.tagId" [name]="'link'+i">
                <option [ngValue]="''">(choose tag)</option>
                @for (id of tagIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
              <label class="w">duration <input type="number" [(ngModel)]="l.duration" [name]="'ld'+i" /></label>
              <button class="small danger" (click)="model.cooldownLinks.splice(i,1)">✕</button>
            </div>
          } @empty { <p class="muted">No cooldown links — this ability uses the GCD.</p> }
        </section>

        <section>
          <div class="rowhead"><h3>Effects (applied in order)</h3><button (click)="addEffect()">+ Effect</button></div>
          @for (e of model.effects; track $index; let i = $index) {
            <div class="row">
              <select [(ngModel)]="e.effectType" [name]="'et'+i">
                <option value="damage">Damage</option>
                <option value="heal">Heal</option>
              </select>
              <label class="w">amount <input type="number" [(ngModel)]="e.baseAmount" [name]="'ea'+i" /></label>
              <select [(ngModel)]="e.scalingStat" [name]="'es'+i">
                @for (stat of scalingStats; track $index; let si = $index) { <option [ngValue]="si">{{ stat }}</option> }
              </select>
              <label class="w">factor <input type="number" step="0.1" [(ngModel)]="e.scalingFactor" [name]="'ef'+i" /></label>
              <button class="small danger" (click)="model.effects.splice(i,1)">✕</button>
            </div>
          } @empty { <p class="muted">No effects — casting this ability does nothing yet.</p> }
        </section>
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .row { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .row select { flex: 1; }
    .row .w { display: flex; flex-direction: column; width: 90px; margin: 0; }
    .row .w input { width: 100%; }
  `],
})
export class AbilityEditor implements OnInit {
  private readonly api = inject(AbilityService);
  private readonly tagApi = inject(AbilityTagService);

  readonly abilities = signal<Ability[]>([]);
  readonly tagIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: Ability | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = ABILITY_GRID_COLUMNS;
  readonly searchFields = ABILITY_SEARCH_FIELDS;
  readonly scalingStats = SCALING_STATS;

  ngOnInit(): void {
    this.reload();
    this.tagApi.getAll().subscribe({ next: rows => this.tagIds.set(rows.map(r => r.tagId)) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.abilities.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newAbility(): void { this.model = emptyAbility(); this.isNew = true; this.modalOpen = true; }

  select(a: Ability): void {
    this.model = {
      ...a,
      tagIds: [...a.tagIds],
      cooldownLinks: a.cooldownLinks.map(x => ({ ...x })),
      effects: a.effects.map(x => ({ ...x })),
    };
    this.isNew = false;
    this.modalOpen = true;
  }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

  addTag(): void { this.model?.tagIds.push(''); }
  addLink(): void { this.model?.cooldownLinks.push({ tagId: '', duration: 3 }); }
  addEffect(): void { this.model?.effects.push({ effectType: 'damage', baseAmount: 0, scalingStat: 0, scalingFactor: 0 }); }

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
    this.api.delete(this.model.abilityId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'An ability with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
