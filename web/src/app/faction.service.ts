import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's FactionDto (M2.6) — a faction plus its ally/hostile ids and race defaults. */
export interface RaceDefault { race: string; score: number; }
export interface Faction {
  factionId: string;
  factionName: string;
  allyIds: string[];
  hostileIds: string[];
  raceDefaults: RaceDefault[];
}

/** One standing on the shared ladder. */
export interface Threshold { name: string; minScore: number; sortOrder: number; considerText: string; }

export function emptyFaction(): Faction {
  return { factionId: '', factionName: '', allyIds: [], hostileIds: [], raceDefaults: [] };
}

/** Grid columns for the Faction index (2.1.1 AF5). */
export const FACTION_GRID_COLUMNS: GridColumn<Faction>[] = [
  { header: 'ID', accessor: f => f.factionId },
  { header: 'Name', accessor: f => f.factionName },
  { header: 'Allies', accessor: f => f.allyIds.length },
  { header: 'Hostiles', accessor: f => f.hostileIds.length },
  { header: 'Race defaults', accessor: f => f.raceDefaults.length },
];
export const FACTION_SEARCH_FIELDS: (keyof Faction)[] = ['factionId', 'factionName'];

@Injectable({ providedIn: 'root' })
export class FactionService {
  private readonly base = `${environment.apiBase}/api/factions`;
  private readonly thresholdsUrl = `${environment.apiBase}/api/thresholds`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<Faction[]> { return this.http.get<Faction[]>(this.base); }
  create(f: Faction): Observable<Faction> { return this.http.post<Faction>(this.base, f); }
  update(f: Faction): Observable<Faction> { return this.http.put<Faction>(`${this.base}/${f.factionId}`, f); }
  delete(factionId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${factionId}`); }

  getThresholds(): Observable<Threshold[]> { return this.http.get<Threshold[]>(this.thresholdsUrl); }
  putThresholds(t: Threshold[]): Observable<Threshold[]> { return this.http.put<Threshold[]>(this.thresholdsUrl, t); }
}
