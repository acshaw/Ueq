import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's Race (M2.10). */
export interface Race {
  raceId: string;
  raceName: string;
  xpModifier: number;
  strMod: number;
  staMod: number;
  agiMod: number;
  dexMod: number;
  intMod: number;
  wisMod: number;
  chaMod: number;
}

export function emptyRace(): Race {
  return {
    raceId: '', raceName: '', xpModifier: 1,
    strMod: 0, staMod: 0, agiMod: 0, dexMod: 0, intMod: 0, wisMod: 0, chaMod: 0,
  };
}

export const RACE_GRID_COLUMNS: GridColumn<Race>[] = [
  { header: 'ID', accessor: r => r.raceId },
  { header: 'Name', accessor: r => r.raceName },
  { header: 'XP modifier', accessor: r => r.xpModifier },
];
export const RACE_SEARCH_FIELDS: (keyof Race)[] = ['raceId', 'raceName'];

@Injectable({ providedIn: 'root' })
export class RaceService {
  private readonly base = 'http://localhost:5144/api/races';
  private readonly http = inject(HttpClient);

  getAll(): Observable<Race[]> { return this.http.get<Race[]>(this.base); }
  create(r: Race): Observable<Race> { return this.http.post<Race>(this.base, r); }
  update(r: Race): Observable<Race> { return this.http.put<Race>(`${this.base}/${r.raceId}`, r); }
  delete(raceId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${raceId}`); }
}
