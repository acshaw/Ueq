import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's AbilityDto (M2.9) — an ability with its three ordered child lists. */
export interface AbilityCooldownLink { tagId: string; duration: number; }
export interface AbilityEffect { effectType: string; baseAmount: number; scalingStat: number; scalingFactor: number; }

export interface Ability {
  abilityId: string;
  displayName: string;
  description: string;
  targetingType: number; // 0=Self, 1=SingleTarget
  range: number;
  castTime: number;
  manaCost: number;
  animTrigger: string;
  tagIds: string[];
  cooldownLinks: AbilityCooldownLink[];
  effects: AbilityEffect[];
}

export function emptyAbility(): Ability {
  return {
    abilityId: '', displayName: '', description: '',
    targetingType: 1, range: 20, castTime: 0, manaCost: 0, animTrigger: '',
    tagIds: [], cooldownLinks: [], effects: [],
  };
}

/** ScalingStatType enum (Combat/AbilityEffect.cs) — shared by damage/heal effect rows. */
export const SCALING_STATS = ['None', 'Str', 'Sta', 'Agi', 'Dex', 'Int', 'Wis', 'Cha'];

/** Grid columns for the Ability index (2.1.1 AF5 — colocated with the type's own service). */
export const ABILITY_GRID_COLUMNS: GridColumn<Ability>[] = [
  { header: 'ID', accessor: a => a.abilityId },
  { header: 'Name', accessor: a => a.displayName },
  { header: 'Targeting', accessor: a => (a.targetingType === 0 ? 'Self' : 'Single Target') },
  { header: 'Mana', accessor: a => a.manaCost },
  { header: 'Effects', accessor: a => a.effects.length },
];
export const ABILITY_SEARCH_FIELDS: (keyof Ability)[] = ['abilityId', 'displayName'];

/** HTTP client for the abilities endpoints. */
@Injectable({ providedIn: 'root' })
export class AbilityService {
  private readonly base = 'http://localhost:5144/api/abilities';
  private readonly http = inject(HttpClient);

  getAll(): Observable<Ability[]> { return this.http.get<Ability[]>(this.base); }
  create(a: Ability): Observable<Ability> { return this.http.post<Ability>(this.base, a); }
  update(a: Ability): Observable<Ability> { return this.http.put<Ability>(`${this.base}/${a.abilityId}`, a); }
  delete(abilityId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${abilityId}`); }
}
