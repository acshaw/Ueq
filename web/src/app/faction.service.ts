import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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
export interface Threshold { name: string; minScore: number; sortOrder: number; }

export function emptyFaction(): Faction {
  return { factionId: '', factionName: '', allyIds: [], hostileIds: [], raceDefaults: [] };
}

@Injectable({ providedIn: 'root' })
export class FactionService {
  private readonly base = 'http://localhost:5144/api/factions';
  private readonly thresholdsUrl = 'http://localhost:5144/api/thresholds';
  private readonly http = inject(HttpClient);

  getAll(): Observable<Faction[]> { return this.http.get<Faction[]>(this.base); }
  create(f: Faction): Observable<Faction> { return this.http.post<Faction>(this.base, f); }
  update(f: Faction): Observable<Faction> { return this.http.put<Faction>(`${this.base}/${f.factionId}`, f); }
  delete(factionId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${factionId}`); }

  getThresholds(): Observable<Threshold[]> { return this.http.get<Threshold[]>(this.thresholdsUrl); }
  putThresholds(t: Threshold[]): Observable<Threshold[]> { return this.http.put<Threshold[]>(this.thresholdsUrl, t); }
}
