import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { XpLevel, XpService } from './xp.service';

/**
 * The web XP Table Editor (M2.7). The single shared level curve: each row is the XP to advance from that
 * level to the next. Row count = max level. A running cumulative total is shown for reference.
 */
@Component({
  selector: 'app-xp-editor',
  imports: [FormsModule, DecimalPipe],
  template: `
    <h1>XP curve</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <p class="muted">XP to advance from each level to the next. Row count = max level ({{ rows().length }}).</p>

    <div class="grid head"><span>Level</span><span>XP to next</span><span>Cumulative</span><span></span></div>
    @for (r of rows(); track $index; let i = $index) {
      <div class="grid">
        <span class="lvl">{{ i + 1 }}</span>
        <input type="number" [(ngModel)]="r.xpToNext" [name]="'xp'+i" />
        <span class="cum">{{ cumulative(i) | number }}</span>
        <button class="small danger" (click)="removeRow(i)">✕</button>
      </div>
    }

    <div class="actions">
      <button (click)="addRow()">+ Add level</button>
      <button class="primary" (click)="save()">Save curve</button>
    </div>
  `,
  styles: [`
    :host { display: block; max-width: 560px; }
    h1 { font-size: 1.2rem; }
    .grid { display: grid; grid-template-columns: 60px 1fr 1fr 40px; gap: 0.5rem; align-items: center; margin-bottom: 0.3rem; }
    .grid.head { font-weight: 600; color: #444; font-size: 0.85rem; margin: 0.5rem 0; }
    .lvl { font-weight: 600; }
    .cum { color: #888; font-size: 0.85rem; }
    input { padding: 0.35rem; box-sizing: border-box; width: 100%; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.75rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    button.small { padding: 0.2rem 0.5rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; font-size: 0.85rem; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
})
export class XpEditor implements OnInit {
  private readonly api = inject(XpService);

  readonly rows = signal<XpLevel[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.getAll().subscribe({
      next: rows => { this.rows.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  cumulative(index: number): number {
    let sum = 0;
    const r = this.rows();
    for (let i = 0; i <= index && i < r.length; i++) sum += r[i].xpToNext || 0;
    return sum;
  }

  addRow(): void { this.rows.update(r => [...r, { level: r.length + 1, xpToNext: 0 }]); }
  removeRow(i: number): void { this.rows.update(r => r.filter((_, j) => j !== i)); }

  save(): void {
    this.api.replace(this.rows()).subscribe({
      next: rows => { this.rows.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
