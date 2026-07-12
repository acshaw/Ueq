import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Mirrors the `items` row / .NET API `Item` (M2.2). camelCase to match the API's JSON. */
export interface Item {
  itemId: string;
  displayName: string;
  description: string;
  maxStackSize: number;

  isEquippable: boolean;
  equipSlot: number;      // EquipSlot enum

  bonusStr: number;
  bonusSta: number;
  bonusAgi: number;
  bonusDex: number;
  bonusInt: number;
  bonusWis: number;
  bonusCha: number;

  weaponBaseDamage: number;
  weaponDelay: number;
  weaponRange: number;
  weaponCategory: number; // WeaponCategory enum

  buyPrice: number;
  sellPrice: number;

  lore: boolean;          // 3.2.1 — max one in possession (EQ1-style)

  iconAddress: string | null;
  updatedAt?: string;
}

/** A new item with sensible defaults (matches ItemDefinition's defaults). */
export function emptyItem(): Item {
  return {
    itemId: '', displayName: '', description: '', maxStackSize: 1,
    isEquippable: false, equipSlot: 11,
    bonusStr: 0, bonusSta: 0, bonusAgi: 0, bonusDex: 0, bonusInt: 0, bonusWis: 0, bonusCha: 0,
    weaponBaseDamage: 10, weaponDelay: 2, weaponRange: 3, weaponCategory: 0,
    buyPrice: 0, sellPrice: 0, lore: false, iconAddress: null,
  };
}

/** HTTP client for the items endpoints. The reference shape later content services copy. */
@Injectable({ providedIn: 'root' })
export class ItemService {
  private readonly base = 'http://localhost:5144/api/items';
  private readonly http = inject(HttpClient);

  getAll(): Observable<Item[]> {
    return this.http.get<Item[]>(this.base);
  }

  create(item: Item): Observable<Item> {
    return this.http.post<Item>(this.base, item);
  }

  update(item: Item): Observable<Item> {
    return this.http.put<Item>(`${this.base}/${item.itemId}`, item);
  }

  delete(itemId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${itemId}`);
  }
}
