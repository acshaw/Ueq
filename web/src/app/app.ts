import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ContentPing, ContentPingService } from './content-ping.service';

/**
 * The content_ping editor (2.1 smoke). Proves the Angular → API → Postgres → Unity chain:
 * rows authored here persist via the .NET API into Postgres, and Unity's ContentLoader logs
 * them at host start. This is the reference editor shape the real content editors (items = 2.2)
 * will copy and flesh out.
 */
@Component({
  selector: 'app-root',
  imports: [FormsModule],
  template: `
    <main>
      <h1>Ueq Content — content_ping</h1>
      <p class="hint">2.1 smoke editor. Rows persist to Postgres via the .NET API; Unity logs them at host start.</p>

      <form class="add" (submit)="add($event)">
        <input [(ngModel)]="newLabel" name="newLabel" placeholder="New label…" required />
        <button type="submit" [disabled]="!newLabel.trim()">Add</button>
        <button type="button" (click)="reload()">Refresh</button>
      </form>

      @if (error()) {
        <p class="error">{{ error() }}</p>
      }

      <table>
        <thead>
          <tr><th>ID</th><th>Label</th><th>Updated</th><th></th></tr>
        </thead>
        <tbody>
          @for (row of rows(); track row.id) {
            <tr>
              <td>{{ row.id }}</td>
              <td>
                @if (editingId() === row.id) {
                  <input [(ngModel)]="editLabel" name="editLabel" />
                } @else {
                  {{ row.label }}
                }
              </td>
              <td class="muted">{{ row.updatedAt }}</td>
              <td class="actions">
                @if (editingId() === row.id) {
                  <button (click)="save(row.id)">Save</button>
                  <button (click)="cancelEdit()">Cancel</button>
                } @else {
                  <button (click)="beginEdit(row)">Edit</button>
                  <button (click)="remove(row.id)">Delete</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="4" class="muted">No rows yet — add one above.</td></tr>
          }
        </tbody>
      </table>
    </main>
  `,
  styles: [`
    main { max-width: 720px; margin: 2rem auto; font-family: system-ui, sans-serif; }
    h1 { font-size: 1.3rem; }
    .hint { color: #666; margin-top: -0.5rem; }
    .add { display: flex; gap: 0.5rem; margin: 1rem 0; }
    .add input { flex: 1; padding: 0.4rem; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 0.4rem 0.5rem; border-bottom: 1px solid #eee; }
    .muted { color: #999; font-size: 0.85rem; }
    .actions { display: flex; gap: 0.35rem; }
    .error { color: #c00; }
    button { cursor: pointer; }
  `]
})
export class App implements OnInit {
  private readonly api = inject(ContentPingService);

  readonly rows = signal<ContentPing[]>([]);
  readonly editingId = signal<number | null>(null);
  readonly error = signal<string | null>(null);
  newLabel = '';
  editLabel = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.rows.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  add(event: Event): void {
    event.preventDefault();
    const label = this.newLabel.trim();
    if (!label) return;
    this.api.create(label).subscribe({
      next: () => { this.newLabel = ''; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  beginEdit(row: ContentPing): void {
    this.editingId.set(row.id);
    this.editLabel = row.label;
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  save(id: number): void {
    this.api.update(id, this.editLabel.trim()).subscribe({
      next: () => { this.editingId.set(null); this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(id: number): void {
    this.api.delete(id).subscribe({
      next: () => this.reload(),
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    return e?.message ?? 'Request failed.';
  }
}
