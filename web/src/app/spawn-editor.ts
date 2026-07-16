import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SpawnTable, SpawnService, emptySpawnTable, SPAWN_GRID_COLUMNS, SPAWN_SEARCH_FIELDS } from './spawn.service';
import { MobService } from './mob.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Spawn Table Editor (M2.7.2, retrofitted onto the 2.1.1 admin framework). A spawn table is a
 * weighted list of mob entries (each with a group size) plus a respawn timer (base ± variance). A
 * SpawnPoint referencing this table weighted-picks an entry on activation and spawns group_size DB
 * mobs, respawning after the timer when the group dies.
 */
@Component({
  selector: 'app-spawn-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Spawn tables</h1>
      <button class="primary" (click)="newTable()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="tables()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New spawn table' : (model?.spawnTableId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.spawnTableId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <label>spawn_table_id <input [(ngModel)]="model.spawnTableId" name="spawnTableId" [disabled]="!isNew" placeholder="Mob Spawn Table" /></label>
          <label>Display name <input [(ngModel)]="model.displayName" name="displayName" /></label>
        </section>

        <section>
          <h3>Respawn timer</h3>
          <p class="muted">Seconds before the camp respawns after its mobs die.</p>
          <div class="trow">
            <label class="w">base <input type="number" [(ngModel)]="model.timerBaseSeconds" name="timerBase" /></label>
            <label class="w">± variance <input type="number" [(ngModel)]="model.timerVariance" name="timerVar" /></label>
          </div>
        </section>

        <section>
          <div class="rowhead"><h3>Entries</h3><button (click)="addEntry()">+ Entry</button></div>
          <p class="muted">Weighted pick; group size = mobs spawned per activation.</p>
          @for (e of model.entries; track $index; let i = $index) {
            <div class="erow">
              <select [(ngModel)]="e.mobId" [name]="'mob'+i">
                <option [ngValue]="''">(choose mob)</option>
                @for (id of mobIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
              <label class="w">weight <input type="number" [(ngModel)]="e.weight" [name]="'w'+i" /></label>
              <label class="w">group <input type="number" [(ngModel)]="e.groupSize" [name]="'g'+i" /></label>
              <button class="small danger" (click)="model.entries.splice(i,1)">✕</button>
            </div>
          } @empty { <p class="muted">No entries — nothing will spawn.</p> }
        </section>
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .erow, .trow { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .erow select { flex: 1; }
    .erow .w, .trow .w { display: flex; flex-direction: column; width: 90px; margin: 0; }
    .erow .w input, .trow .w input { width: 100%; }
  `],
})
export class SpawnEditor implements OnInit {
  private readonly api = inject(SpawnService);
  private readonly mobApi = inject(MobService);

  readonly tables = signal<SpawnTable[]>([]);
  readonly mobIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: SpawnTable | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = SPAWN_GRID_COLUMNS;
  readonly searchFields = SPAWN_SEARCH_FIELDS;

  ngOnInit(): void {
    this.reload();
    this.mobApi.getAll().subscribe({ next: rows => this.mobIds.set(rows.map(r => r.mobId)) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.tables.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newTable(): void { this.model = emptySpawnTable(); this.isNew = true; this.modalOpen = true; }

  select(t: SpawnTable): void {
    this.model = { ...t, entries: t.entries.map(e => ({ ...e })) };
    this.isNew = false;
    this.modalOpen = true;
  }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

  addEntry(): void { this.model?.entries.push({ mobId: '', weight: 1, groupSize: 1 }); }

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
    this.api.delete(this.model.spawnTableId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A spawn table with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
