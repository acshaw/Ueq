import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Mirrors the API's SpawnTableDto (M2.7.2) — a weighted table with an inlined respawn timer. */
export interface SpawnEntry { mobId: string; weight: number; groupSize: number; }
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

@Injectable({ providedIn: 'root' })
export class SpawnService {
  private readonly base = 'http://localhost:5144/api/spawn-tables';
  private readonly http = inject(HttpClient);

  getAll(): Observable<SpawnTable[]> { return this.http.get<SpawnTable[]>(this.base); }
  create(t: SpawnTable): Observable<SpawnTable> { return this.http.post<SpawnTable>(this.base, t); }
  update(t: SpawnTable): Observable<SpawnTable> { return this.http.put<SpawnTable>(`${this.base}/${t.spawnTableId}`, t); }
  delete(spawnTableId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${spawnTableId}`); }
}
