import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Faction, RaceDefault, Threshold, FactionService, emptyFaction, FACTION_GRID_COLUMNS, FACTION_SEARCH_FIELDS,
} from './faction.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Faction Editor (M2.6, retrofitted onto the 2.1.1 admin framework). A faction has a display
 * name, NPC-to-NPC ally/hostile relations (toggled against the other factions), and race→faction
 * starting scores. The single shared standing ladder (thresholds) used by every faction is a separate
 * panel — not a per-row entity, so it isn't part of the faction grid (2.1.1 AF10).
 */
@Component({
  selector: 'app-faction-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Factions</h1>
      <span>
        <button (click)="editThresholds()">⚖ Shared thresholds</button>
        <button class="primary" (click)="newFaction()">+ New</button>
      </span>
    </div>
    @if (error() && !modalOpen && !thresholdsOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="factions()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New faction' : (model?.factionId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.factionId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <label>faction_id <input [(ngModel)]="model.factionId" name="factionId" [disabled]="!isNew" placeholder="CityGuards" /></label>
          <label>Display name <input [(ngModel)]="model.factionName" name="factionName" placeholder="City Guards" /></label>
        </section>

        <section>
          <h3>NPC relations</h3>
          <p class="muted">How this faction regards other factions (social aggro / guards-respond).</p>
          @for (other of otherFactions(); track other.factionId) {
            <div class="relrow">
              <span class="relname">{{ other.factionId }}</span>
              <label class="check"><input type="checkbox"
                [checked]="model.allyIds.includes(other.factionId)"
                (change)="toggleRelation('ally', other.factionId, $any($event.target).checked)" /> Ally</label>
              <label class="check"><input type="checkbox"
                [checked]="model.hostileIds.includes(other.factionId)"
                (change)="toggleRelation('hostile', other.factionId, $any($event.target).checked)" /> Hostile</label>
            </div>
          } @empty {
            <p class="muted">No other factions to relate to yet.</p>
          }
        </section>

        <section>
          <h3>Race defaults</h3>
          <p class="muted">A new character of this race starts at this standing score with this faction.</p>
          @for (d of model.raceDefaults; track $index; let i = $index) {
            <div class="rdrow">
              <input [(ngModel)]="d.race" [name]="'rdrace'+i" placeholder="Troll" />
              <input type="number" [(ngModel)]="d.score" [name]="'rdscore'+i" placeholder="0" />
              <button class="small danger" (click)="removeRaceDefault(i)">✕</button>
            </div>
          }
          <button (click)="addRaceDefault()">+ Add race default</button>
        </section>
      }
    </app-crud-modal>

    <app-crud-modal
      [open]="thresholdsOpen"
      title="Shared thresholds"
      [isNew]="true"
      [error]="thresholdsOpen ? error() : null"
      (save)="saveThresholds()"
      (close)="closeThresholds()"
    >
      <p class="muted">The named standing ladder every faction evaluates against, low → high.</p>
      <p class="muted">Consider text is what a player sees pressing C / right-clicking a mob at this
        standing — shown as "&lt;target&gt; &lt;text&gt;." (5.4).</p>
      @for (t of thresholds(); track $index; let i = $index) {
        <div class="trow">
          <input [(ngModel)]="t.name" [name]="'tn'+i" placeholder="Indifferent" />
          <input type="number" [(ngModel)]="t.minScore" [name]="'tm'+i" placeholder="0" />
          <input [(ngModel)]="t.considerText" [name]="'tc'+i" placeholder="regards you indifferently" class="considertext" />
          <button class="small" (click)="moveThreshold(i,-1)" [disabled]="i===0">↑</button>
          <button class="small" (click)="moveThreshold(i,1)" [disabled]="i===thresholds().length-1">↓</button>
          <button class="small danger" (click)="removeThreshold(i)">✕</button>
        </div>
      }
      <button (click)="addThreshold()">+ Add standing</button>
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .toolbar span { display: flex; gap: 0.5rem; }
    .relrow { display: flex; align-items: center; gap: 1rem; padding: 0.25rem 0; }
    .relname { flex: 1; font-weight: 600; font-size: 0.9rem; }
    .check { display: flex; gap: 0.3rem; align-items: center; margin: 0; }
    .check input { width: auto; }
    .rdrow, .trow { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .rdrow input:first-child, .trow input:first-child { flex: 1; }
    .trow input.considertext { flex: 2; }
  `],
})
export class FactionEditor implements OnInit {
  private readonly api = inject(FactionService);

  readonly factions = signal<Faction[]>([]);
  readonly thresholds = signal<Threshold[]>([]);
  readonly error = signal<string | null>(null);
  model: Faction | null = null;
  isNew = false;
  modalOpen = false;
  thresholdsOpen = false;

  readonly columns = FACTION_GRID_COLUMNS;
  readonly searchFields = FACTION_SEARCH_FIELDS;

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.factions.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  otherFactions(): Faction[] {
    return this.factions().filter(f => f.factionId !== this.model?.factionId);
  }

  newFaction(): void {
    this.thresholdsOpen = false;
    this.model = emptyFaction();
    this.isNew = true;
    this.modalOpen = true;
  }

  select(f: Faction): void {
    this.thresholdsOpen = false;
    this.model = {
      ...f,
      allyIds: [...f.allyIds],
      hostileIds: [...f.hostileIds],
      raceDefaults: f.raceDefaults.map(d => ({ ...d })),
    };
    this.isNew = false;
    this.modalOpen = true;
  }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

  toggleRelation(kind: 'ally' | 'hostile', otherId: string, on: boolean): void {
    if (!this.model) return;
    const list = kind === 'ally' ? this.model.allyIds : this.model.hostileIds;
    const idx = list.indexOf(otherId);
    if (on && idx < 0) list.push(otherId);
    if (!on && idx >= 0) list.splice(idx, 1);
  }

  addRaceDefault(): void { this.model?.raceDefaults.push({ race: '', score: 0 } as RaceDefault); }
  removeRaceDefault(i: number): void { this.model?.raceDefaults.splice(i, 1); }

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
    this.api.delete(this.model.factionId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  // ── Shared thresholds (AF10 — its own small panel, not a per-row grid entity) ────────────────────
  editThresholds(): void {
    this.modalOpen = false;
    this.model = null;
    this.thresholdsOpen = true;
    this.api.getThresholds().subscribe({
      next: rows => this.thresholds.set(rows),
      error: err => this.error.set(this.describe(err)),
    });
  }

  closeThresholds(): void { this.thresholdsOpen = false; this.error.set(null); }

  addThreshold(): void { this.thresholds.update(t => [...t, { name: '', minScore: 0, sortOrder: t.length, considerText: '' }]); }
  removeThreshold(i: number): void { this.thresholds.update(t => t.filter((_, j) => j !== i)); }
  moveThreshold(i: number, dir: number): void {
    const a = [...this.thresholds()];
    const j = i + dir;
    if (j < 0 || j >= a.length) return;
    [a[i], a[j]] = [a[j], a[i]];
    this.thresholds.set(a);
  }
  saveThresholds(): void {
    this.api.putThresholds(this.thresholds()).subscribe({
      next: rows => { this.thresholds.set(rows); this.error.set(null); this.thresholdsOpen = false; },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A faction with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
