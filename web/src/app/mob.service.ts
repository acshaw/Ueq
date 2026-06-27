import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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
  };
}

@Injectable({ providedIn: 'root' })
export class MobService {
  private readonly base = 'http://localhost:5144/api/mobs';
  private readonly http = inject(HttpClient);

  getAll(): Observable<Mob[]> { return this.http.get<Mob[]>(this.base); }
  create(m: Mob): Observable<Mob> { return this.http.post<Mob>(this.base, m); }
  update(m: Mob): Observable<Mob> { return this.http.put<Mob>(`${this.base}/${m.mobId}`, m); }
  delete(mobId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${mobId}`); }
}
