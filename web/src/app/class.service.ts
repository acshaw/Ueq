import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's ClassDto (M2.10) — a class with its ordered starting-ability id list. Weapon-prop
 * cosmetic fields deliberately don't appear here (RC4 — they live on the Unity-side CharacterRoster). */
export interface Class {
  classId: string;
  className: string;
  xpModifier: number;

  baseStr: number; baseSta: number; baseAgi: number; baseDex: number;
  baseInt: number; baseWis: number; baseCha: number;

  classBaseHP: number; hpPerLevel: number; staCap: number;
  baseStaRatio: number; staGrowthRate: number;

  manaStatType: number; // 0 None, 1 Intellect, 2 Wisdom
  classBaseMana: number; manaPerLevel: number; manaCap: number;
  baseManaRatio: number; manaGrowthRate: number;

  tierL1Miss: number; tierL1Glancing: number; tierL1Hit: number;
  tierL1Solid: number; tierL1Good: number; tierL1Critical: number; tierL1Crippling: number;

  tierL20Miss: number; tierL20Glancing: number; tierL20Hit: number;
  tierL20Solid: number; tierL20Good: number; tierL20Critical: number; tierL20Crippling: number;

  startingAbilityIds: string[];
}

export function emptyClass(): Class {
  return {
    classId: '', className: '', xpModifier: 1,
    baseStr: 10, baseSta: 10, baseAgi: 10, baseDex: 10, baseInt: 10, baseWis: 10, baseCha: 10,
    classBaseHP: 15, hpPerLevel: 4, staCap: 255, baseStaRatio: 0.23, staGrowthRate: 0.15,
    manaStatType: 0, classBaseMana: 0, manaPerLevel: 0, manaCap: 0, baseManaRatio: 0.23, manaGrowthRate: 0,
    tierL1Miss: 17.5, tierL1Glancing: 40, tierL1Hit: 30, tierL1Solid: 10, tierL1Good: 2.5, tierL1Critical: 0, tierL1Crippling: 0,
    tierL20Miss: 2, tierL20Glancing: 13, tierL20Hit: 20, tierL20Solid: 35, tierL20Good: 25, tierL20Critical: 3, tierL20Crippling: 2,
    startingAbilityIds: [],
  };
}

/** ManaStatType enum (Combat/ClassDefinition.cs). */
export const MANA_STAT_TYPES = ['None', 'Intellect', 'Wisdom'];

export const CLASS_GRID_COLUMNS: GridColumn<Class>[] = [
  { header: 'ID', accessor: c => c.classId },
  { header: 'Name', accessor: c => c.className },
  { header: 'Base HP', accessor: c => c.classBaseHP },
  { header: 'Mana stat', accessor: c => MANA_STAT_TYPES[c.manaStatType] ?? c.manaStatType },
  { header: 'Starting abilities', accessor: c => c.startingAbilityIds.length },
];
export const CLASS_SEARCH_FIELDS: (keyof Class)[] = ['classId', 'className'];

@Injectable({ providedIn: 'root' })
export class ClassService {
  private readonly base = `${environment.apiBase}/api/classes`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<Class[]> { return this.http.get<Class[]>(this.base); }
  create(c: Class): Observable<Class> { return this.http.post<Class>(this.base, c); }
  update(c: Class): Observable<Class> { return this.http.put<Class>(`${this.base}/${c.classId}`, c); }
  delete(classId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${classId}`); }
}
