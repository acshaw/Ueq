import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/**
 * Mirrors the API's WorldPlacement entity (2.7.3). Unlike every other content type here, Unity's Editor
 * sync/import tools are the actual authors of these rows (devplan WP4) — this service only lists them and
 * (for SpawnPoint) edits the non-spatial `data` config (WP7). There is no `create` — a placement's position
 * has no meaningful value from a web form, so placements only ever originate in Unity.
 */
export interface WorldPlacement {
  placementId: string;
  zoneId: string;
  markerType: string;
  posX: number | null;
  posY: number | null;
  posZ: number | null;
  rotY: number;
  data: string; // raw JSON text — parsed/edited client-side per marker type
  updatedAt: string;
}

/** SpawnPoint's `data` shape (mirrors SpawnPoint.CapturePlacementData in Unity) — the only marker type
 * with web-editable fields (WP7). PatrolRoute/WanderRegion data is shown read-only only. */
export interface SpawnPointPlacementData {
  spawnTableId: string;
  mobId: string;
  activationRadius: number;
  snapToGround: boolean;
  navSampleRadius: number;
  freeRange: boolean;
  freeRangeRadius: number;
  patrolRoutePlacementId: string | null;
  wanderRegionPlacementId: string | null;
}

export function emptySpawnPointData(): SpawnPointPlacementData {
  return {
    spawnTableId: '', mobId: '', activationRadius: 50, snapToGround: true, navSampleRadius: 8,
    freeRange: false, freeRangeRadius: 400, patrolRoutePlacementId: null, wanderRegionPlacementId: null,
  };
}

/** A short, marker-type-aware summary for the grid — never throws on malformed/unexpected JSON. */
export function summarizePlacement(p: WorldPlacement): string {
  let d: Record<string, unknown>;
  try { d = JSON.parse(p.data || '{}'); } catch { return '(unreadable data)'; }

  switch (p.markerType) {
    case 'SpawnPoint':
      return (d['spawnTableId'] as string) || (d['mobId'] as string) || '(nothing configured)';
    case 'PatrolRoute': {
      const points = d['points'] as unknown[] | undefined;
      return `${points?.length ?? 0} waypoint(s)${d['loop'] ? ', looped' : ''}`;
    }
    case 'WanderRegion':
      return `${d['shape'] ?? ''}`;
    default:
      return '';
  }
}

export function formatPosition(p: WorldPlacement): string {
  if (p.posX == null || p.posY == null || p.posZ == null) return '';
  return `${p.posX.toFixed(1)}, ${p.posY.toFixed(1)}, ${p.posZ.toFixed(1)}`;
}

export const PLACEMENT_GRID_COLUMNS: GridColumn<WorldPlacement>[] = [
  { header: 'Zone', accessor: p => p.zoneId },
  { header: 'Type', accessor: p => p.markerType },
  { header: 'Summary', accessor: p => summarizePlacement(p) },
  { header: 'Position', accessor: p => formatPosition(p) },
];
export const PLACEMENT_SEARCH_FIELDS: (keyof WorldPlacement)[] = ['zoneId', 'markerType'];

@Injectable({ providedIn: 'root' })
export class PlacementService {
  private readonly base = `${environment.apiBase}/api/world-placements`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<WorldPlacement[]> { return this.http.get<WorldPlacement[]>(this.base); }
  update(p: WorldPlacement): Observable<WorldPlacement> {
    return this.http.put<WorldPlacement>(`${this.base}/${p.placementId}`, p);
  }
  delete(placementId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${placementId}`);
  }
}
