import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's SpawnTableDto (M2.7.2) — a weighted table with an inlined respawn timer. 8.1.4
 * adds four calendar-aware spawn conditions ('Any'/null = no restriction). 8.2 adds an optional
 * per-entry respawn pace override (null = use the table's timer). 8.3 adds an optional per-entry level
 * range (both null = no variance, spawns at the mob's authored level). */
export interface SpawnEntry {
  mobId: string;
  weight: number;
  groupSize: number;
  timeCondition: string;        // 'Any' | 'DayOnly' | 'NightOnly'
  lunarCondition: string;       // 'Any' | 'FullMoonOnly' | 'NewMoonOnly'
  dayOfWeekCondition: number | null; // 1-7, null = Any
  monthCondition: number | null;     // 1-13, null = Any
  respawnBaseSeconds: number | null; // 8.2 — null = use the table's timer
  respawnVariance: number | null;    // 8.2 — null = use the table's timer
  minLevelOverride: number | null;   // 8.3 — null = no variance (mob's authored level)
  maxLevelOverride: number | null;   // 8.3 — null = no variance (mob's authored level)
}
export interface SpawnTable {
  spawnTableId: string;
  displayName: string;
  timerBaseSeconds: number;
  timerVariance: number;
  entries: SpawnEntry[];
}

export function emptySpawnTable(): SpawnTable {
  return { spawnTableId: '', displayName: '', timerBaseSeconds: 300, timerVariance: 0, entries: [] };
}

/** Grid columns for the Spawn Table index (2.1.1 AF5). */
export const SPAWN_GRID_COLUMNS: GridColumn<SpawnTable>[] = [
  { header: 'ID', accessor: t => t.spawnTableId },
  { header: 'Name', accessor: t => t.displayName },
  { header: 'Entries', accessor: t => t.entries.length },
  { header: 'Timer (s)', accessor: t => t.timerBaseSeconds },
];
export const SPAWN_SEARCH_FIELDS: (keyof SpawnTable)[] = ['spawnTableId', 'displayName'];

@Injectable({ providedIn: 'root' })
export class SpawnService {
  private readonly base = `${environment.apiBase}/api/spawn-tables`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<SpawnTable[]> { return this.http.get<SpawnTable[]>(this.base); }
  create(t: SpawnTable): Observable<SpawnTable> { return this.http.post<SpawnTable>(this.base, t); }
  update(t: SpawnTable): Observable<SpawnTable> { return this.http.put<SpawnTable>(`${this.base}/${t.spawnTableId}`, t); }
  delete(spawnTableId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${spawnTableId}`); }
}
