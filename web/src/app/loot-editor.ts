import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LootTable, LootService, emptyLootTable } from './loot.service';
import { ItemService } from './item.service';

/**
 * The web Loot Table Editor (M2.7). A loot table is three weighted lists: an item pool (item dropdown +
 * weight), a drop-count distribution (count + weight), and coin tiers (min/max copper + weight). On a
 * mob's death the server rolls a drop count, that many items from the pool, and one coin tier.
 */
@Component({
  selector: 'app-loot-editor',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <aside>
        <div class="head">
          <h2>Loot tables</h2>
          <button (click)="newTable()">+ New</button>
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <ul>
          @for (t of tables(); track t.lootTableId) {
            <li [class.active]="model?.lootTableId === t.lootTableId && !isNew" (click)="select(t)">
              <span class="id">{{ t.lootTableId }}</span>
              <span class="muted">{{ t.items.length }} item(s)</span>
            </li>
          } @empty {
            <li class="muted">No loot tables — click “New”.</li>
          }
        </ul>
      </aside>

      <main>
        @if (model) {
          <h1>{{ isNew ? 'New loot table' : model.lootTableId }}</h1>

          <section>
            <h3>Identity</h3>
            <label>loot_table_id <input [(ngModel)]="model.lootTableId" name="lootTableId" [disabled]="!isNew" placeholder="Giant Rat Loot Table" /></label>
            <label>Display name <input [(ngModel)]="model.displayName" name="displayName" /></label>
          </section>

          <section>
            <div class="rowhead"><h3>Item pool</h3><button (click)="addItem()">+ Item</button></div>
            @for (it of model.items; track $index; let i = $index) {
              <div class="row">
                <select [(ngModel)]="it.itemId" [name]="'item'+i">
                  <option [ngValue]="''">(choose item)</option>
                  @for (id of itemIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
                </select>
                <label class="w">weight <input type="number" [(ngModel)]="it.weight" [name]="'iw'+i" /></label>
                <button class="small danger" (click)="model.items.splice(i,1)">✕</button>
              </div>
            } @empty { <p class="muted">No items — nothing will drop.</p> }
          </section>

          <section>
            <div class="rowhead"><h3>Drop counts</h3><button (click)="addDrop()">+ Count</button></div>
            <p class="muted">How many item rolls happen, weighted (e.g. 0×50, 1×30, 2×15, 3×5).</p>
            @for (d of model.dropCounts; track $index; let i = $index) {
              <div class="row">
                <label class="w">count <input type="number" [(ngModel)]="d.count" [name]="'dc'+i" /></label>
                <label class="w">weight <input type="number" [(ngModel)]="d.weight" [name]="'dw'+i" /></label>
                <button class="small danger" (click)="model.dropCounts.splice(i,1)">✕</button>
              </div>
            } @empty { <p class="muted">No drop counts — nothing will drop.</p> }
          </section>

          <section>
            <div class="rowhead"><h3>Coin tiers</h3><button (click)="addCoin()">+ Tier</button></div>
            <p class="muted">Weighted copper drop; set min = max for a fixed amount.</p>
            @for (c of model.coinTiers; track $index; let i = $index) {
              <div class="row">
                <label class="w">min <input type="number" [(ngModel)]="c.minCopper" [name]="'cmin'+i" /></label>
                <label class="w">max <input type="number" [(ngModel)]="c.maxCopper" [name]="'cmax'+i" /></label>
                <label class="w">weight <input type="number" [(ngModel)]="c.weight" [name]="'cw'+i" /></label>
                <button class="small danger" (click)="model.coinTiers.splice(i,1)">✕</button>
              </div>
            } @empty { <p class="muted">No coin tiers — no coin drops.</p> }
          </section>

          <div class="actions">
            <button class="primary" (click)="save()" [disabled]="isNew && !model.lootTableId.trim()">Save</button>
            @if (!isNew) { <button class="danger" (click)="remove()">Delete</button> }
          </div>
        } @else {
          <p class="muted">Select a loot table or create a new one.</p>
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
    .row { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .row select { flex: 1; }
    .row .w { display: flex; flex-direction: column; width: 80px; }
    input, select { padding: 0.35rem; box-sizing: border-box; }
    .row .w input { width: 100%; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    button.small { padding: 0.2rem 0.5rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; font-size: 0.85rem; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
})
export class LootEditor implements OnInit {
  private readonly api = inject(LootService);
  private readonly itemApi = inject(ItemService);

  readonly tables = signal<LootTable[]>([]);
  readonly itemIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: LootTable | null = null;
  isNew = false;

  ngOnInit(): void {
    this.reload();
    this.itemApi.getAll().subscribe({ next: rows => this.itemIds.set(rows.map(r => r.itemId)) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.tables.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newTable(): void { this.model = emptyLootTable(); this.isNew = true; }

  select(t: LootTable): void {
    this.model = {
      ...t,
      items: t.items.map(x => ({ ...x })),
      dropCounts: t.dropCounts.map(x => ({ ...x })),
      coinTiers: t.coinTiers.map(x => ({ ...x })),
    };
    this.isNew = false;
  }

  addItem(): void { this.model?.items.push({ itemId: '', weight: 1 }); }
  addDrop(): void { this.model?.dropCounts.push({ count: 0, weight: 1 }); }
  addCoin(): void { this.model?.coinTiers.push({ minCopper: 0, maxCopper: 0, weight: 1 }); }

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
    this.api.delete(this.model.lootTableId).subscribe({
      next: () => { this.model = null; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A loot table with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
