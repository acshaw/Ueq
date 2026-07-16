import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormsModule } from '@angular/forms';

/** One column of a `ContentGrid`. `accessor` reads the display value straight off a row. */
export interface GridColumn<T> {
  header: string;
  accessor: (row: T) => string | number;
  sortable?: boolean;   // default true
  kind?: 'text' | 'icon'; // 'icon' renders a small thumbnail from the accessor's value (2.1.1 AF7)
}

/**
 * Generic admin index grid (2.1.1) — a full-width, client-side searchable/sortable table over
 * `rows`, driven entirely by a per-type `columns` schema. No pagination: content-type row counts
 * here are dev-authored (dozens, not thousands) — see the devplan's AF6 for when that'd change.
 * Emits the clicked row's full object; the parent decides what "select" means (open its own modal).
 */
@Component({
  selector: 'app-content-grid',
  imports: [FormsModule],
  template: `
    <div class="toolbar">
      <input class="search" type="text" [(ngModel)]="query" placeholder="Search…" />
      <span class="count muted">{{ filtered().length }} of {{ rows.length }}</span>
    </div>
    <table>
      <thead>
        <tr>
          @for (col of columns; track col.header; let i = $index) {
            <th [class.sortable]="col.sortable !== false" (click)="sortBy(i)">
              {{ col.header }}
              @if (sortCol === i) { <span class="arrow">{{ sortAsc ? '▲' : '▼' }}</span> }
            </th>
          }
        </tr>
      </thead>
      <tbody>
        @for (row of filtered(); track $index) {
          <tr (click)="rowClick.emit(row)">
            @for (col of columns; track col.header) {
              @if (col.kind === 'icon') {
                <td class="icon-cell">
                  @if (col.accessor(row)) {
                    <img [src]="col.accessor(row)" [title]="col.accessor(row)" alt="" />
                  }
                </td>
              } @else {
                <td>{{ col.accessor(row) }}</td>
              }
            }
          </tr>
        } @empty {
          <tr><td class="muted" [attr.colspan]="columns.length">
            {{ rows.length === 0 ? 'Nothing here yet.' : 'No rows match your search.' }}
          </td></tr>
        }
      </tbody>
    </table>
  `,
  styles: [`
    :host { display: block; }
    .toolbar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
    .search { max-width: 260px; padding: 0.4rem 0.6rem; box-sizing: border-box; border: 1px solid #ddd;
              border-radius: 4px; }
    .count { font-size: 0.8rem; }
    table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
    th, td { text-align: left; padding: 0.5rem 0.6rem; border-bottom: 1px solid #eee; }
    th { color: #444; font-weight: 600; font-size: 0.82rem; user-select: none; white-space: nowrap; }
    th.sortable { cursor: pointer; }
    th.sortable:hover { color: #1a73e8; }
    .arrow { font-size: 0.7em; margin-left: 0.2rem; }
    tbody tr { cursor: pointer; }
    tbody tr:hover { background: #f7f9fb; }
    .icon-cell img { width: 24px; height: 24px; object-fit: contain; vertical-align: middle; }
    .muted { color: #999; }
  `],
})
export class ContentGrid<T> {
  @Input() rows: T[] = [];
  @Input() columns: GridColumn<T>[] = [];
  @Input() searchFields: (keyof T)[] = [];
  @Output() rowClick = new EventEmitter<T>();

  query = '';
  sortCol = -1;
  sortAsc = true;

  sortBy(i: number): void {
    if (this.columns[i]?.sortable === false) return;
    if (this.sortCol === i) this.sortAsc = !this.sortAsc;
    else { this.sortCol = i; this.sortAsc = true; }
  }

  filtered(): T[] {
    let list = this.rows;

    const q = this.query.trim().toLowerCase();
    if (q) {
      list = list.filter(row =>
        this.searchFields.some(f => String(row[f] ?? '').toLowerCase().includes(q)));
    }

    if (this.sortCol >= 0 && this.columns[this.sortCol]) {
      const accessor = this.columns[this.sortCol].accessor;
      list = [...list].sort((a, b) => {
        const av = accessor(a), bv = accessor(b);
        const cmp = typeof av === 'number' && typeof bv === 'number'
          ? av - bv
          : String(av).localeCompare(String(bv));
        return this.sortAsc ? cmp : -cmp;
      });
    }

    return list;
  }
}
