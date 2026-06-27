import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SpawnTable, SpawnService, emptySpawnTable } from './spawn.service';
import { MobService } from './mob.service';

/**
 * The web Spawn Table Editor (M2.7.2). A spawn table is a weighted list of mob entries (each with a group
 * size) plus a respawn timer (base ± variance). A SpawnPoint referencing this table weighted-picks an
 * entry on activation and spawns group_size DB mobs, respawning after the timer when the group dies.
 */
@Component({
  selector: 'app-spawn-editor',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <aside>
        <div class="head">
          <h2>Spawn tables</h2>
          <button (click)="newTable()">+ New</button>
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <ul>
          @for (t of tables(); track t.spawnTableId) {
            <li [class.active]="model?.spawnTableId === t.spawnTableId && !isNew" (click)="select(t)">
              <span class="id">{{ t.spawnTableId }}</span>
              <span class="muted">{{ t.entries.length }} entry(ies)</span>
            </li>
          } @empty {
            <li class="muted">No spawn tables — click “New”.</li>
          }
        </ul>
      </aside>

      <main>
        @if (model) {
          <h1>{{ isNew ? 'New spawn table' : model.spawnTableId }}</h1>

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

          <div class="actions">
            <button class="primary" (click)="save()" [disabled]="isNew && !model.spawnTableId.trim()">Save</button>
            @if (!isNew) { <button class="danger" (click)="remove()">Delete</button> }
          </div>
        } @else {
          <p class="muted">Select a spawn table or create a new one.</p>
        }
      </main>
    </div>
  `,
  styles: [`
    .wrap { display: flex; gap: 1rem; }
    aside { width: 220px; flex-shrink: 0; border-right: 1px solid #eee; padding-right: 1rem; }
    .head { display: flex; justify-content: space-between; align-items: center; }
    aside ul { list-style: none; padding: 0; margin: 0.5rem 0 0; }
    aside li { padding: 0.4rem 0.5rem; cursor: pointer; border-radius: 4px; display: flex; flex-direction: column; }
    aside li:hover { background: #f4f4f4; }
    aside li.active { background: #e8f0fe; }
    .id { font-weight: 600; font-size: 0.9rem; }
    main { flex: 1; }
    h1 { font-size: 1.2rem; }
    section { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 0.75rem; }
    .rowhead { display: flex; justify-content: space-between; align-items: center; }
    h3 { margin: 0 0 0.4rem; font-size: 0.95rem; color: #444; }
    label { font-size: 0.82rem; color: #555; }
    .erow, .trow { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .erow select { flex: 1; }
    .w { display: flex; flex-direction: column; width: 90px; }
    input, select { padding: 0.35rem; box-sizing: border-box; }
    .w input { width: 100%; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    button.small { padding: 0.2rem 0.5rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; font-size: 0.85rem; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
})
export class SpawnEditor implements OnInit {
  private readonly api = inject(SpawnService);
  private readonly mobApi = inject(MobService);

  readonly tables = signal<SpawnTable[]>([]);
  readonly mobIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: SpawnTable | null = null;
  isNew = false;

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

  newTable(): void { this.model = emptySpawnTable(); this.isNew = true; }

  select(t: SpawnTable): void {
    this.model = { ...t, entries: t.entries.map(e => ({ ...e })) };
    this.isNew = false;
  }

  addEntry(): void { this.model?.entries.push({ mobId: '', weight: 1, groupSize: 1 }); }

  save(): void {
    if (!this.model) return;
    const call = this.isNew ? this.api.create(this.model) : this.api.update(this.model);
    call.subscribe({
      next: saved => { this.isNew = false; this.model = saved; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model || this.isNew) return;
    this.api.delete(this.model.spawnTableId).subscribe({
      next: () => { this.model = null; this.reload(); },
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
