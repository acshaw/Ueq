import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { WorldClockSettings, WorldClockSettingsService } from './world-clock-settings.service';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * Web editor for the single shared day-length/lunar-cycle/fog config (5.12 follow-up). A plain settings
 * form rather than a ContentGrid/CrudModal — like the Faction thresholds sub-panel, there's exactly one
 * row here, not an id-keyed list, so a grid has nothing to index (2.1.1, AF9/AF10 precedent).
 */
@Component({
  selector: 'app-world-clock-editor',
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="toolbar"><h1>World Clock</h1></div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (saved()) { <p class="saved">Saved.</p> }

    <p class="muted">
      Controls the day/night cycle, lunar phase pacing, and distance fog for every zone. Takes effect the
      next time the game server (re)starts — it's read once at boot, not live.
    </p>

    <section>
      <h3>Day / night cycle</h3>
      <label>
        Day length (real-world minutes per full in-game day/night cycle)
        <input type="number" min="1" step="0.5" [(ngModel)]="settings().dayLengthMinutes"
               (ngModelChange)="update('dayLengthMinutes', $event)" name="dayLengthMinutes" />
      </label>
    </section>

    <section>
      <h3>Lunar cycle</h3>
      <label>
        Lunar cycle length (in-game days from new moon to new moon — a real month is ~28-29.5)
        <input type="number" min="0.5" step="0.5" [(ngModel)]="settings().lunarCycleDays"
               (ngModelChange)="update('lunarCycleDays', $event)" name="lunarCycleDays" />
      </label>
      <p class="warn">
        = {{ realMinutesPerLunarCycle() | number: '1.0-1' }} real-world minutes per full lunar cycle at the
        current day length.
      </p>
    </section>

    <section>
      <h3>Distance fog</h3>
      <p class="muted">
        Fades terrain/props/mobs into haze with distance instead of letting players see across the whole
        zone. Color auto-matches the sky's horizon color at the current time of day — only the distances
        are set here.
      </p>
      <div class="grid">
        <label>
          Start distance (fog begins fading geometry here)
          <input type="number" min="0" step="10" [(ngModel)]="settings().fogStartDistance"
                 (ngModelChange)="update('fogStartDistance', $event)" name="fogStartDistance" />
        </label>
        <label>
          End distance (geometry is fully hidden by here)
          <input type="number" min="10" step="10" [(ngModel)]="settings().fogEndDistance"
                 (ngModelChange)="update('fogEndDistance', $event)" name="fogEndDistance" />
        </label>
      </div>
      @if (settings().fogEndDistance <= settings().fogStartDistance) {
        <p class="warn">End distance should be well past the start distance, or fog reads as a hard cutoff
          instead of a gradual fade.</p>
      }
      <p class="warn">
        For mob spawns to actually pop in hidden by fog rather than in plain view, each SpawnPoint's
        Activation Radius (Unity Inspector, not here) needs to sit at or inside the end distance above.
      </p>
    </section>

    <div class="actions">
      <button class="primary" (click)="save()">Save</button>
    </div>
  `,
  styles: [ADMIN_STYLES, `.saved { color: #2e7d32; font-size: 0.85rem; }`]
})
export class WorldClockEditor implements OnInit {
  private readonly api = inject(WorldClockSettingsService);

  readonly settings = signal<WorldClockSettings>({
    id: 1, dayLengthMinutes: 50, lunarCycleDays: 8, fogStartDistance: 120, fogEndDistance: 520,
  });
  readonly error = signal<string | null>(null);
  readonly saved = signal(false);

  ngOnInit(): void {
    this.api.get().subscribe({
      next: s => { this.settings.set(s); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  update<K extends keyof WorldClockSettings>(field: K, value: WorldClockSettings[K]): void {
    this.settings.update(s => ({ ...s, [field]: value }));
    this.saved.set(false);
  }

  realMinutesPerLunarCycle(): number {
    const s = this.settings();
    return (s.dayLengthMinutes || 0) * (s.lunarCycleDays || 0);
  }

  save(): void {
    this.api.update(this.settings()).subscribe({
      next: s => { this.settings.set(s); this.error.set(null); this.saved.set(true); },
      error: err => { this.error.set(this.describe(err)); this.saved.set(false); },
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
