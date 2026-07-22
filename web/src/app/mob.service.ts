import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/** One faction standing change applied to the killer when this mob dies (M2.7.1). */
export interface MobFactionHit { factionId: string; delta: number; }

/** Mirrors the API's Mob (M2.5). References other content by id. */
export interface Mob {
  mobId: string;
  displayName: string;
  mobLevel: number;
  prefabAddress: string | null;

  maxHealth: number;
  attackDamage: number;
  attackInterval: number;
  attackRange: number;

  movementType: number; // 0 Stationary, 1 Wander
  moveSpeed: number;
  wanderRadius: number;
  wanderPauseMin: number;
  wanderPauseMax: number;

  perceptionRadius: number;
  baseAggroThreat: number;

  factionId: string | null;
  aggroMaxStanding: string;
  warningMaxStanding: string;

  conversationSetId: string | null;
  lootTableId: string | null;
  xpReward: number;

  vendorId: string | null;
  vendorOpenKeyword: string;

  factionHits: MobFactionHit[];

  // 5.1.1 (HR5) / 5.1.2 (AV3) / 2.12 (SK5) — combat pipeline data, authored per mob.
  weaponCategory: number; // WeaponCategory: 0 Might, 1 Finesse
  weaponSkill: number;
  tierMiss: number;
  tierGlancing: number;
  tierHit: number;
  tierSolid: number;
  tierGood: number;
  tierCritical: number;
  tierCrippling: number;
  attackIsParryable: boolean;
  avoidanceAgility: number;
  avoidanceDexterity: number;

  updatedAt?: string;
}

export function emptyMob(): Mob {
  return {
    mobId: '', displayName: '', mobLevel: 1, prefabAddress: 'Enemy',
    maxHealth: 10, attackDamage: 1, attackInterval: 2, attackRange: 2,
    movementType: 1, moveSpeed: 3.5, wanderRadius: 10, wanderPauseMin: 2, wanderPauseMax: 6,
    perceptionRadius: 20, baseAggroThreat: 1,
    factionId: null, aggroMaxStanding: 'Threatening', warningMaxStanding: 'Apprehensive',
    conversationSetId: null, lootTableId: null, xpReward: 0,
    vendorId: null, vendorOpenKeyword: 'wares',
    factionHits: [],
    // Defaults mirror the Warrior Level 1 starting table (design doc §2.5) — a new mob starts from a
    // valid, non-degenerate hit-tier table.
    weaponCategory: 0, weaponSkill: 0,
    tierMiss: 17.5, tierGlancing: 40, tierHit: 30, tierSolid: 10, tierGood: 2.5, tierCritical: 0, tierCrippling: 0,
    attackIsParryable: true, avoidanceAgility: 20, avoidanceDexterity: 20,
  };
}

/** Grid columns for the Mob index (2.1.1 AF5). */
export const MOB_GRID_COLUMNS: GridColumn<Mob>[] = [
  { header: 'ID', accessor: m => m.mobId },
  { header: 'Name', accessor: m => m.displayName },
  { header: 'Level', accessor: m => m.mobLevel },
  { header: 'Faction', accessor: m => m.factionId ?? '' },
  { header: 'XP', accessor: m => m.xpReward },
];
export const MOB_SEARCH_FIELDS: (keyof Mob)[] = ['mobId', 'displayName'];

@Injectable({ providedIn: 'root' })
export class MobService {
  private readonly base = `${environment.apiBase}/api/mobs`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<Mob[]> { return this.http.get<Mob[]>(this.base); }
  create(m: Mob): Observable<Mob> { return this.http.post<Mob>(this.base, m); }
  update(m: Mob): Observable<Mob> { return this.http.put<Mob>(`${this.base}/${m.mobId}`, m); }
  delete(mobId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${mobId}`); }
}
