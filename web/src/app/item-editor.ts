import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Item, ItemService, emptyItem } from './item.service';

/**
 * The web Item Editor (M2.2). Master/detail: pick an item on the left, edit its sections on the right.
 * The reference editor shape every later content editor copies.
 */
@Component({
  selector: 'app-item-editor',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <aside>
        <div class="head">
          <h2>Items</h2>
          <button (click)="newItem()">+ New</button>
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <ul>
          @for (it of items(); track it.itemId) {
            <li [class.active]="model?.itemId === it.itemId && !isNew" (click)="select(it)">
              <span class="id">{{ it.itemId }}</span>
              <span class="muted">{{ it.displayName }}</span>
            </li>
          } @empty {
            <li class="muted">No items — click “New”.</li>
          }
        </ul>
      </aside>

      <main>
        @if (model) {
          <h1>{{ isNew ? 'New item' : model.itemId }}</h1>

          <section>
            <h3>Identity</h3>
            <label>item_id <input [(ngModel)]="model.itemId" name="itemId" [disabled]="!isNew" placeholder="iron_sword" /></label>
            <label>Display name <input [(ngModel)]="model.displayName" name="displayName" /></label>
            <label>Description <textarea [(ngModel)]="model.description" name="description" rows="2"></textarea></label>
            <label>Max stack size <input type="number" [(ngModel)]="model.maxStackSize" name="maxStackSize" min="1" /></label>
          </section>

          <section>
            <h3>Equipment</h3>
            <label class="check"><input type="checkbox" [(ngModel)]="model.isEquippable" name="isEquippable" /> Equippable</label>
            <label>Equip slot
              <select [(ngModel)]="model.equipSlot" name="equipSlot" [disabled]="!model.isEquippable">
                @for (s of equipSlots; track s.v) { <option [ngValue]="s.v">{{ s.n }}</option> }
              </select>
            </label>
          </section>

          <section>
            <h3>Stat Bonuses</h3>
            <div class="grid">
              <label>STR <input type="number" [(ngModel)]="model.bonusStr" name="bonusStr" /></label>
              <label>STA <input type="number" [(ngModel)]="model.bonusSta" name="bonusSta" /></label>
              <label>AGI <input type="number" [(ngModel)]="model.bonusAgi" name="bonusAgi" /></label>
              <label>DEX <input type="number" [(ngModel)]="model.bonusDex" name="bonusDex" /></label>
              <label>INT <input type="number" [(ngModel)]="model.bonusInt" name="bonusInt" /></label>
              <label>WIS <input type="number" [(ngModel)]="model.bonusWis" name="bonusWis" /></label>
              <label>CHA <input type="number" [(ngModel)]="model.bonusCha" name="bonusCha" /></label>
            </div>
          </section>

          <section>
            <h3>Weapon Stats</h3>
            <div class="grid">
              <label>Base damage <input type="number" [(ngModel)]="model.weaponBaseDamage" name="weaponBaseDamage" /></label>
              <label>Delay (s) <input type="number" step="0.1" [(ngModel)]="model.weaponDelay" name="weaponDelay" /></label>
              <label>Range <input type="number" step="0.1" [(ngModel)]="model.weaponRange" name="weaponRange" /></label>
              <label>Category
                <select [(ngModel)]="model.weaponCategory" name="weaponCategory">
                  @for (c of weaponCategories; track c.v) { <option [ngValue]="c.v">{{ c.n }}</option> }
                </select>
              </label>
            </div>
          </section>

          <section>
            <h3>Economy</h3>
            <div class="grid">
              <label>Buy price (copper) <input type="number" [(ngModel)]="model.buyPrice" name="buyPrice" /></label>
              <label>Sell price (copper) <input type="number" [(ngModel)]="model.sellPrice" name="sellPrice" /></label>
            </div>
          </section>

          <section>
            <h3>Icon</h3>
            <label>Addressables address <input [(ngModel)]="model.iconAddress" name="iconAddress" placeholder="icon_iron_sword" /></label>
          </section>

          <div class="actions">
            <button class="primary" (click)="save()" [disabled]="isNew && !model.itemId.trim()">Save</button>
            @if (!isNew) { <button class="danger" (click)="remove()">Delete</button> }
          </div>
        } @else {
          <p class="muted">Select an item or create a new one.</p>
        }
      </main>
    </div>
  `,
  styles: [`
    .wrap { display: flex; gap: 1rem; }
    aside { width: 240px; flex-shrink: 0; border-right: 1px solid #eee; padding-right: 1rem; }
    .head { display: flex; justify-content: space-between; align-items: center; }
    aside ul { list-style: none; padding: 0; margin: 0.5rem 0 0; }
    aside li { padding: 0.4rem 0.5rem; cursor: pointer; border-radius: 4px; display: flex; flex-direction: column; }
    aside li:hover { background: #f4f4f4; }
    aside li.active { background: #e8f0fe; }
    .id { font-weight: 600; font-size: 0.9rem; }
    main { flex: 1; }
    h1 { font-size: 1.2rem; }
    section { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 0.75rem; }
    section h3 { margin: 0 0 0.5rem; font-size: 0.95rem; color: #444; }
    label { display: block; margin: 0.35rem 0; font-size: 0.85rem; color: #555; }
    label.check { display: flex; gap: 0.4rem; align-items: center; }
    input, textarea, select { width: 100%; padding: 0.35rem; box-sizing: border-box; }
    label.check input { width: auto; }
    .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0 0.75rem; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
})
export class ItemEditor implements OnInit {
  private readonly api = inject(ItemService);

  readonly items = signal<Item[]>([]);
  readonly error = signal<string | null>(null);
  model: Item | null = null;
  isNew = false;

  readonly equipSlots = [
    { v: 0, n: 'Head' }, { v: 1, n: 'Chest' }, { v: 2, n: 'Legs' }, { v: 3, n: 'Hands' },
    { v: 4, n: 'Feet' }, { v: 5, n: 'Back' }, { v: 6, n: 'Neck' }, { v: 7, n: 'Ring 1' },
    { v: 8, n: 'Ring 2' }, { v: 9, n: 'Ear 1' }, { v: 10, n: 'Ear 2' }, { v: 11, n: 'Weapon' },
    { v: 12, n: 'Offhand' },
  ];
  readonly weaponCategories = [{ v: 0, n: 'Might' }, { v: 1, n: 'Finesse' }];

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.items.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newItem(): void { this.model = emptyItem(); this.isNew = true; }

  select(it: Item): void { this.model = { ...it }; this.isNew = false; }

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
    this.api.delete(this.model.itemId).subscribe({
      next: () => { this.model = null; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'An item with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
