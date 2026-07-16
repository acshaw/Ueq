import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Item, ItemService, emptyItem, ITEM_GRID_COLUMNS, ITEM_SEARCH_FIELDS } from './item.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Item Editor (M2.2, retrofitted onto the 2.1.1 admin framework). A full-width searchable/
 * sortable grid is the index; "+ New" or clicking a row opens the same form content as before,
 * inside the shared CRUD modal.
 */
@Component({
  selector: 'app-item-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Items</h1>
      <button class="primary" (click)="newItem()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="items()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New item' : (model?.itemId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.itemId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
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
          <h3>Flags</h3>
          <label class="check"><input type="checkbox" [(ngModel)]="model.lore" name="lore" (ngModelChange)="onLoreChange()" /> LORE — can only carry one</label>
          @if (model.lore && model.maxStackSize > 1) {
            <p class="warn">LORE items are limited to one in possession — stack size is treated as 1.</p>
          }
        </section>

        <section>
          <h3>Icon</h3>
          <label>Addressables address <input [(ngModel)]="model.iconAddress" name="iconAddress" placeholder="icon_iron_sword" /></label>
        </section>
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES],
})
export class ItemEditor implements OnInit {
  private readonly api = inject(ItemService);

  readonly items = signal<Item[]>([]);
  readonly error = signal<string | null>(null);
  model: Item | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = ITEM_GRID_COLUMNS;
  readonly searchFields = ITEM_SEARCH_FIELDS;

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

  newItem(): void { this.model = emptyItem(); this.isNew = true; this.modalOpen = true; }

  // 3.2.1: LORE implies max-one — collapse stack size when it's toggled on (guard for decision L4).
  onLoreChange(): void {
    if (this.model?.lore && this.model.maxStackSize > 1) this.model.maxStackSize = 1;
  }

  select(it: Item): void { this.model = { ...it }; this.isNew = false; this.modalOpen = true; }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

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
    this.api.delete(this.model.itemId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
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
