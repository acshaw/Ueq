import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Vendor, VendorService, emptyVendor } from './vendor.service';
import { Item, ItemService } from './item.service';

/**
 * The web Vendor Editor (M2.3). A vendor is a display name + an ordered list of item ids it sells
 * (prices come from the items themselves). Item rows are dropdowns populated from the items endpoint,
 * so you can only stock items that exist in the DB (which is also what the game can resolve).
 */
@Component({
  selector: 'app-vendor-editor',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <aside>
        <div class="head">
          <h2>Vendors</h2>
          <button (click)="newVendor()">+ New</button>
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <ul>
          @for (v of vendors(); track v.vendorId) {
            <li [class.active]="model?.vendorId === v.vendorId && !isNew" (click)="select(v)">
              <span class="id">{{ v.vendorId }}</span>
              <span class="muted">{{ v.displayName }} · {{ v.itemIds.length }} item(s)</span>
            </li>
          } @empty {
            <li class="muted">No vendors — click “New”.</li>
          }
        </ul>
      </aside>

      <main>
        @if (model) {
          <h1>{{ isNew ? 'New vendor' : model.vendorId }}</h1>

          <section>
            <h3>Identity</h3>
            <label>vendor_id <input [(ngModel)]="model.vendorId" name="vendorId" [disabled]="!isNew" placeholder="general_store" /></label>
            <label>Display name <input [(ngModel)]="model.displayName" name="displayName" placeholder="General Store" /></label>
          </section>

          <section>
            <div class="head">
              <h3>Wares (sold to players)</h3>
              <button (click)="addRow()" [disabled]="items().length === 0">+ Add item</button>
            </div>
            @if (items().length === 0) {
              <p class="muted">No items exist yet — create some in the Items editor first.</p>
            }
            @for (id of model.itemIds; track $index; let i = $index) {
              <div class="row">
                <span class="rownum">{{ i + 1 }}</span>
                <select [ngModel]="model.itemIds[i]" (ngModelChange)="setRow(i, $event)" [name]="'row' + i">
                  @for (it of items(); track it.itemId) {
                    <option [value]="it.itemId">{{ it.itemId }} — {{ it.displayName }} ({{ it.buyPrice }}c)</option>
                  }
                </select>
                <button class="small" (click)="moveRow(i, -1)" [disabled]="i === 0">↑</button>
                <button class="small" (click)="moveRow(i, 1)" [disabled]="i === model.itemIds.length - 1">↓</button>
                <button class="small danger" (click)="removeRow(i)">✕</button>
              </div>
            } @empty {
              <p class="muted">No wares yet.</p>
            }
          </section>

          <div class="actions">
            <button class="primary" (click)="save()" [disabled]="isNew && !model.vendorId.trim()">Save</button>
            @if (!isNew) { <button class="danger" (click)="remove()">Delete</button> }
          </div>
        } @else {
          <p class="muted">Select a vendor or create a new one.</p>
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
    section h3 { margin: 0; font-size: 0.95rem; color: #444; }
    label { display: block; margin: 0.35rem 0; font-size: 0.85rem; color: #555; }
    input, select { padding: 0.35rem; box-sizing: border-box; }
    label input { width: 100%; }
    .row { display: flex; align-items: center; gap: 0.4rem; margin: 0.35rem 0; }
    .row select { flex: 1; }
    .rownum { width: 1.2rem; color: #999; font-size: 0.8rem; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    button.small { padding: 0.2rem 0.5rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
})
export class VendorEditor implements OnInit {
  private readonly api = inject(VendorService);
  private readonly itemApi = inject(ItemService);

  readonly vendors = signal<Vendor[]>([]);
  readonly items = signal<Item[]>([]);
  readonly error = signal<string | null>(null);
  model: Vendor | null = null;
  isNew = false;

  ngOnInit(): void {
    this.reload();
    this.itemApi.getAll().subscribe({ next: rows => this.items.set(rows) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.vendors.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newVendor(): void { this.model = emptyVendor(); this.isNew = true; }

  select(v: Vendor): void { this.model = { ...v, itemIds: [...v.itemIds] }; this.isNew = false; }

  addRow(): void {
    if (!this.model) return;
    const first = this.items()[0]?.itemId ?? '';
    this.model.itemIds = [...this.model.itemIds, first];
  }

  setRow(i: number, itemId: string): void {
    if (!this.model) return;
    this.model.itemIds = this.model.itemIds.map((v, idx) => (idx === i ? itemId : v));
  }

  removeRow(i: number): void {
    if (!this.model) return;
    this.model.itemIds = this.model.itemIds.filter((_, idx) => idx !== i);
  }

  moveRow(i: number, dir: number): void {
    if (!this.model) return;
    const j = i + dir;
    const arr = [...this.model.itemIds];
    if (j < 0 || j >= arr.length) return;
    [arr[i], arr[j]] = [arr[j], arr[i]];
    this.model.itemIds = arr;
  }

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
    this.api.delete(this.model.vendorId).subscribe({
      next: () => { this.model = null; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A vendor with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
