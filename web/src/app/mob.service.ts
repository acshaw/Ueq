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

  // 5.4 (AG3) — social aggro, opt-in per mob.
  socialAggroEnabled: boolean;
  socialAggroRadius: number;

  factionId: string | null;
  aggroMaxStanding: string;
  warningMaxStanding: string;

  conversationSetId: string | null;
  lootTableId: string | null;
  xpReward: number;

  vendorId: string | null;
  vendorOpenKeyword: string;

  factionHits: MobFactionHit[];

  // 5.1.1 (HR5) / 5.1.2 (AV3) / 2.12 (SK5) — combat pipeline data, authored per mob. weaponCategory/
  // weaponSkill are no longer consumed by the resolver as of 5.1.5 (superseded by atk) — kept for now,
  // candidates for a future cleanup pass.
  weaponCategory: number; // WeaponCategory: 0 Might, 1 Finesse
  weaponSkill: number;
  // 5.1.5 (AD3) — this mob's ATK, authored directly as one number (replaces the old 7-field tier table).
  atk: number;
  attackIsParryable: boolean;
  avoidanceDodge: number;
  avoidanceParry: number;
  avoidanceRiposte: number;
  // 2026-08-21 (Mitigation) — this mob's AC, authored directly as one number (same treatment as atk).
  ac: number;

  updatedAt?: string;
}

export function emptyMob(): Mob {
  return {
    mobId: '', displayName: '', mobLevel: 1, prefabAddress: 'Enemy',
    maxHealth: 10, attackDamage: 1, attackInterval: 2, attackRange: 2,
    movementType: 1, moveSpeed: 3.5, wanderRadius: 10, wanderPauseMin: 2, wanderPauseMax: 6,
    perceptionRadius: 20, baseAggroThreat: 1,
    socialAggroEnabled: false, socialAggroRadius: 20,
    factionId: null, aggroMaxStanding: 'Threatening', warningMaxStanding: 'Apprehensive',
    conversationSetId: null, lootTableId: null, xpReward: 0,
    vendorId: null, vendorOpenKeyword: 'wares',
    factionHits: [],
    weaponCategory: 0, weaponSkill: 0,
    // 5.1.5 (AD3) — defaults to the shared curve's MinAtk floor (see combat-sim/combat-math.ts), the
    // same "start from a sane, non-degenerate baseline" spirit as the old tier-table default.
    atk: 10,
    attackIsParryable: true, avoidanceDodge: 20, avoidanceParry: 20, avoidanceRiposte: 20,
    ac: 0,
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
