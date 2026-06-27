import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Faction, RaceDefault, Threshold, FactionService, emptyFaction } from './faction.service';

/**
 * The web Faction Editor (M2.6). A faction has a display name, NPC-to-NPC ally/hostile relations
 * (toggled against the other factions), and race→faction starting scores. A separate panel edits the
 * single shared standing ladder (thresholds) used by every faction.
 */
@Component({
  selector: 'app-faction-editor',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <aside>
        <div class="head">
          <h2>Factions</h2>
          <button (click)="newFaction()">+ New</button>
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <ul>
          @for (f of factions(); track f.factionId) {
            <li [class.active]="model?.factionId === f.factionId && !isNew && !editingThresholds" (click)="select(f)">
              <span class="id">{{ f.factionId }}</span>
              <span class="muted">{{ f.factionName }}</span>
            </li>
          } @empty {
            <li class="muted">No factions — click “New”.</li>
          }
        </ul>
        <button class="thresholds-btn" [class.active]="editingThresholds" (click)="editThresholds()">⚖ Shared thresholds</button>
      </aside>

      <main>
        @if (editingThresholds) {
          <h1>Shared thresholds</h1>
          <p class="muted">The named standing ladder every faction evaluates against, low → high.</p>
          @for (t of thresholds(); track $index; let i = $index) {
            <div class="trow">
              <input [(ngModel)]="t.name" [name]="'tn'+i" placeholder="Indifferent" />
              <input type="number" [(ngModel)]="t.minScore" [name]="'tm'+i" placeholder="0" />
              <button class="small" (click)="moveThreshold(i,-1)" [disabled]="i===0">↑</button>
              <button class="small" (click)="moveThreshold(i,1)" [disabled]="i===thresholds().length-1">↓</button>
              <button class="small danger" (click)="removeThreshold(i)">✕</button>
            </div>
          }
          <div class="actions">
            <button (click)="addThreshold()">+ Add standing</button>
            <button class="primary" (click)="saveThresholds()">Save thresholds</button>
          </div>
        } @else if (model) {
          <h1>{{ isNew ? 'New faction' : model.factionId }}</h1>

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

          <div class="actions">
            <button class="primary" (click)="save()" [disabled]="isNew && !model.factionId.trim()">Save</button>
            @if (!isNew) { <button class="danger" (click)="remove()">Delete</button> }
          </div>
        } @else {
          <p class="muted">Select a faction, create one, or edit the shared thresholds.</p>
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
    .thresholds-btn { margin-top: 1rem; width: 100%; background: none; border: 1px solid #ddd;
                      border-radius: 4px; padding: 0.4rem; cursor: pointer; color: #555; }
    .thresholds-btn.active { background: #e8f0fe; border-color: #1a73e8; color: #1a73e8; }
    main { flex: 1; }
    h1 { font-size: 1.2rem; }
    section { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 0.75rem; }
    h3 { margin: 0 0 0.5rem; font-size: 0.95rem; color: #444; }
    label { display: block; margin: 0.35rem 0; font-size: 0.82rem; color: #555; }
    input { padding: 0.35rem; box-sizing: border-box; }
    label input { width: 100%; }
    .relrow { display: flex; align-items: center; gap: 1rem; padding: 0.25rem 0; }
    .relname { flex: 1; font-weight: 600; font-size: 0.9rem; }
    .check { display: flex; gap: 0.3rem; align-items: center; margin: 0; }
    .check input { width: auto; }
    .rdrow, .trow { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .rdrow input:first-child, .trow input:first-child { flex: 1; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    button.small { padding: 0.2rem 0.5rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; font-size: 0.85rem; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
})
export class FactionEditor implements OnInit {
  private readonly api = inject(FactionService);

  readonly factions = signal<Faction[]>([]);
  readonly thresholds = signal<Threshold[]>([]);
  readonly error = signal<string | null>(null);
  model: Faction | null = null;
  isNew = false;
  editingThresholds = false;

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

  newFaction(): void { this.editingThresholds = false; this.model = emptyFaction(); this.isNew = true; }

  select(f: Faction): void {
    this.editingThresholds = false;
    this.model = {
      ...f,
      allyIds: [...f.allyIds],
      hostileIds: [...f.hostileIds],
      raceDefaults: f.raceDefaults.map(d => ({ ...d })),
    };
    this.isNew = false;
  }

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
      next: saved => { this.isNew = false; this.model = saved; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model || this.isNew) return;
    this.api.delete(this.model.factionId).subscribe({
      next: () => { this.model = null; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  // ── Shared thresholds ──────────────────────────────────────────────────────
  editThresholds(): void {
    this.editingThresholds = true;
    this.model = null;
    this.api.getThresholds().subscribe({
      next: rows => this.thresholds.set(rows),
      error: err => this.error.set(this.describe(err)),
    });
  }

  addThreshold(): void { this.thresholds.update(t => [...t, { name: '', minScore: 0, sortOrder: t.length }]); }
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
      next: rows => { this.thresholds.set(rows); this.error.set(null); },
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
