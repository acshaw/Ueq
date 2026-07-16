import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Vendor, VendorService, emptyVendor, VENDOR_GRID_COLUMNS, VENDOR_SEARCH_FIELDS } from './vendor.service';
import { Item, ItemService } from './item.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Vendor Editor (M2.3, retrofitted onto the 2.1.1 admin framework). A vendor is a display
 * name + an ordered list of item ids it sells (prices come from the items themselves). Item rows are
 * dropdowns populated from the items endpoint, so you can only stock items that exist in the DB.
 */
@Component({
  selector: 'app-vendor-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Vendors</h1>
      <button class="primary" (click)="newVendor()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="vendors()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New vendor' : (model?.vendorId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.vendorId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <label>vendor_id <input [(ngModel)]="model.vendorId" name="vendorId" [disabled]="!isNew" placeholder="general_store" /></label>
          <label>Display name <input [(ngModel)]="model.displayName" name="displayName" placeholder="General Store" /></label>
        </section>

        <section>
          <div class="rowhead">
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
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .row { display: flex; align-items: center; gap: 0.4rem; margin: 0.35rem 0; }
    .row select { flex: 1; }
    .rownum { width: 1.2rem; color: #999; font-size: 0.8rem; }
  `],
})
export class VendorEditor implements OnInit {
  private readonly api = inject(VendorService);
  private readonly itemApi = inject(ItemService);

  readonly vendors = signal<Vendor[]>([]);
  readonly items = signal<Item[]>([]);
  readonly error = signal<string | null>(null);
  model: Vendor | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = VENDOR_GRID_COLUMNS;
  readonly searchFields = VENDOR_SEARCH_FIELDS;

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

  newVendor(): void { this.model = emptyVendor(); this.isNew = true; this.modalOpen = true; }

  select(v: Vendor): void { this.model = { ...v, itemIds: [...v.itemIds] }; this.isNew = false; this.modalOpen = true; }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

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
      next: saved => { this.isNew = false; this.model = saved; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model || this.isNew) return;
    this.api.delete(this.model.vendorId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
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
