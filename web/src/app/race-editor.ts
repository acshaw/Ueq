import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Race, RaceService, emptyRace, RACE_GRID_COLUMNS, RACE_SEARCH_FIELDS } from './race.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/** The web Race Editor (M2.10) — flat: identity, XP modifier, and 7 stat modifiers. No children. */
@Component({
  selector: 'app-race-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Races</h1>
      <button class="primary" (click)="newRace()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="races()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New race' : (model?.raceId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.raceId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <label>race_id <input [(ngModel)]="model.raceId" name="raceId" [disabled]="!isNew" placeholder="Dwarf" /></label>
          <label>Display name <input [(ngModel)]="model.raceName" name="raceName" /></label>
        </section>

        <section>
          <h3>XP</h3>
          <label>XP modifier <input type="number" step="0.01" [(ngModel)]="model.xpModifier" name="xpModifier" /></label>
        </section>

        <section>
          <h3>Stat modifiers</h3>
          <label>STR <input type="number" [(ngModel)]="model.strMod" name="strMod" /></label>
          <label>STA <input type="number" [(ngModel)]="model.staMod" name="staMod" /></label>
          <label>AGI <input type="number" [(ngModel)]="model.agiMod" name="agiMod" /></label>
          <label>DEX <input type="number" [(ngModel)]="model.dexMod" name="dexMod" /></label>
          <label>INT <input type="number" [(ngModel)]="model.intMod" name="intMod" /></label>
          <label>WIS <input type="number" [(ngModel)]="model.wisMod" name="wisMod" /></label>
          <label>CHA <input type="number" [(ngModel)]="model.chaMod" name="chaMod" /></label>
        </section>
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES],
})
export class RaceEditor implements OnInit {
  private readonly api = inject(RaceService);

  readonly races = signal<Race[]>([]);
  readonly error = signal<string | null>(null);
  model: Race | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = RACE_GRID_COLUMNS;
  readonly searchFields = RACE_SEARCH_FIELDS;

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.races.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newRace(): void { this.model = emptyRace(); this.isNew = true; this.modalOpen = true; }

  select(r: Race): void {
    this.model = { ...r };
    this.isNew = false;
    this.modalOpen = true;
  }

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
    this.api.delete(this.model.raceId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A race with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
