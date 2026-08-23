import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  WorldPlacement, PlacementService, SpawnPointPlacementData, emptySpawnPointData, formatPosition,
  PLACEMENT_GRID_COLUMNS, PLACEMENT_SEARCH_FIELDS,
} from './placement.service';
import { SpawnService } from './spawn.service';
import { MobService } from './mob.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Placement Editor (2.7.3, Stage C). Unlike every other editor here, this one doesn't author
 * content — Unity's `Tools/Zones/Sync Placements to Database` is the only way a row is ever created; this
 * view exists to (a) see every placement across every zone in one list and (b) tweak a `SpawnPoint`'s
 * non-spatial config (spawn table, activation radius, ...) without reopening Unity (WP7). Position,
 * rotation, zone, and marker type are always read-only. `PatrolRoute`/`WanderRegion` rows have no
 * web-editable fields at all — their `data` is spatial (waypoints/shape), which belongs in the Unity Scene
 * view, not a number form.
 */
@Component({
  selector: 'app-placement-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>World placements</h1>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="placements()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="model ? (model.markerType + ' — ' + model.zoneId) : ''"
      [isNew]="false"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="!spawnData"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Placement</h3>
          <p class="muted">Zone, type, position, and rotation are authored in Unity — not editable here.</p>
          <div class="trow">
            <label class="w">Zone <input [value]="model.zoneId" disabled /></label>
            <label class="w">Type <input [value]="model.markerType" disabled /></label>
          </div>
          <div class="trow">
            <label class="w">Position <input [value]="positionText()" disabled /></label>
            <label class="w">Rotation (Y) <input [value]="model.rotY" disabled /></label>
          </div>
        </section>

        @if (spawnData) {
          <section>
            <h3>Spawn config</h3>
            <label>Spawn table (weighted/timed/grouped — takes precedence)
              <select [(ngModel)]="spawnData.spawnTableId" name="spawnTableId">
                <option [ngValue]="''">(none)</option>
                @for (id of spawnTableIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
            </label>
            <label>Single mob (used only if no spawn table is set)
              <select [(ngModel)]="spawnData.mobId" name="mobId">
                <option [ngValue]="''">(none)</option>
                @for (id of mobIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
            </label>
            <div class="trow">
              <label class="w">Activation radius <input type="number" [(ngModel)]="spawnData.activationRadius" name="activationRadius" /></label>
              <label class="w">Nav sample radius <input type="number" [(ngModel)]="spawnData.navSampleRadius" name="navSampleRadius" /></label>
            </div>
            <label class="checkbox"><input type="checkbox" [(ngModel)]="spawnData.snapToGround" name="snapToGround" /> Snap to ground</label>
            <label class="checkbox"><input type="checkbox" [(ngModel)]="spawnData.freeRange" name="freeRange" /> Free-range wander (ignored if a Wander Region is set)</label>
            @if (spawnData.freeRange) {
              <label class="w">Free-range radius <input type="number" [(ngModel)]="spawnData.freeRangeRadius" name="freeRangeRadius" /></label>
            }
            <p class="muted">
              Patrol route: {{ spawnData.patrolRoutePlacementId || 'none' }} · Wander region: {{ spawnData.wanderRegionPlacementId || 'none' }}
              — set in Unity, not editable here.
            </p>
          </section>
        } @else {
          <section>
            <h3>Config</h3>
            <p class="muted">
              {{ model.markerType }} has no web-editable fields — its data is spatial (waypoints/shape).
              Edit it in the Unity Editor, then run <code>Tools/Zones/Sync Placements to Database</code>.
            </p>
            <pre class="rawdata">{{ model.data }}</pre>
          </section>
        }
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .trow { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .trow .w { display: flex; flex-direction: column; flex: 1; margin: 0; }
    .trow .w input { width: 100%; box-sizing: border-box; }
    label.checkbox { display: flex; align-items: center; gap: 0.4rem; margin: 0.4rem 0; }
    label.checkbox input { width: auto; }
    .rawdata { background: #f7f9fb; border: 1px solid #eee; border-radius: 4px; padding: 0.6rem;
               font-size: 0.8rem; white-space: pre-wrap; word-break: break-word; }
  `],
})
export class PlacementEditor implements OnInit {
  private readonly api = inject(PlacementService);
  private readonly spawnApi = inject(SpawnService);
  private readonly mobApi = inject(MobService);

  readonly placements = signal<WorldPlacement[]>([]);
  readonly spawnTableIds = signal<string[]>([]);
  readonly mobIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);

  model: WorldPlacement | null = null;
  spawnData: SpawnPointPlacementData | null = null;
  modalOpen = false;

  readonly columns = PLACEMENT_GRID_COLUMNS;
  readonly searchFields = PLACEMENT_SEARCH_FIELDS;

  ngOnInit(): void {
    this.reload();
    this.spawnApi.getAll().subscribe({ next: rows => this.spawnTableIds.set(rows.map(r => r.spawnTableId)) });
    this.mobApi.getAll().subscribe({ next: rows => this.mobIds.set(rows.map(r => r.mobId)) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.placements.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  select(p: WorldPlacement): void {
    this.model = { ...p };
    this.spawnData = null;
    if (p.markerType === 'SpawnPoint') {
      try { this.spawnData = { ...emptySpawnPointData(), ...JSON.parse(p.data || '{}') }; }
      catch { this.spawnData = emptySpawnPointData(); }
    }
    this.modalOpen = true;
  }

  positionText(): string { return this.model ? formatPosition(this.model) : ''; }

  closeModal(): void { this.modalOpen = false; this.model = null; this.spawnData = null; this.error.set(null); }

  save(): void {
    if (!this.model || !this.spawnData) return;
    this.model.data = JSON.stringify(this.spawnData);
    this.api.update(this.model).subscribe({
      next: () => { this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model) return;
    const ok = window.confirm(
      'This removes the database record only — if this placement is still baked into a running ' +
      "server's scene, it will keep appearing until that scene copy is also removed. Delete anyway?");
    if (!ok) return;
    this.api.delete(this.model.placementId).subscribe({
      next: () => { this.modalOpen = false; this.model = null; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
