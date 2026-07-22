import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's AbilityTag (M2.9) — a flat reference used by an ability's tags + cooldown links. */
export interface AbilityTag {
  tagId: string;
  displayName: string;
}

export function emptyAbilityTag(): AbilityTag {
  return { tagId: '', displayName: '' };
}

export const ABILITY_TAG_GRID_COLUMNS: GridColumn<AbilityTag>[] = [
  { header: 'ID', accessor: t => t.tagId },
  { header: 'Display name', accessor: t => t.displayName },
];
export const ABILITY_TAG_SEARCH_FIELDS: (keyof AbilityTag)[] = ['tagId', 'displayName'];

@Injectable({ providedIn: 'root' })
export class AbilityTagService {
  private readonly base = `${environment.apiBase}/api/ability-tags`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<AbilityTag[]> { return this.http.get<AbilityTag[]>(this.base); }
  create(t: AbilityTag): Observable<AbilityTag> { return this.http.post<AbilityTag>(this.base, t); }
  update(t: AbilityTag): Observable<AbilityTag> { return this.http.put<AbilityTag>(`${this.base}/${t.tagId}`, t); }
  delete(tagId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${tagId}`); }
}
