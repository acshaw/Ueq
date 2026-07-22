import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's LootTableDto (M2.7) — a loot table with its three weighted child lists. */
export interface LootItem { itemId: string; weight: number; }
export interface LootDropCount { count: number; weight: number; }
export interface LootCoinTier { minCopper: number; maxCopper: number; weight: number; }

export interface LootTable {
  lootTableId: string;
  displayName: string;
  items: LootItem[];
  dropCounts: LootDropCount[];
  coinTiers: LootCoinTier[];
}

export function emptyLootTable(): LootTable {
  return { lootTableId: '', displayName: '', items: [], dropCounts: [], coinTiers: [] };
}

/** Grid columns for the Loot Table index (2.1.1 AF5). */
export const LOOT_GRID_COLUMNS: GridColumn<LootTable>[] = [
  { header: 'ID', accessor: t => t.lootTableId },
  { header: 'Name', accessor: t => t.displayName },
  { header: 'Items', accessor: t => t.items.length },
  { header: 'Drop counts', accessor: t => t.dropCounts.length },
  { header: 'Coin tiers', accessor: t => t.coinTiers.length },
];
export const LOOT_SEARCH_FIELDS: (keyof LootTable)[] = ['lootTableId', 'displayName'];

@Injectable({ providedIn: 'root' })
export class LootService {
  private readonly base = `${environment.apiBase}/api/loot-tables`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<LootTable[]> { return this.http.get<LootTable[]>(this.base); }
  create(t: LootTable): Observable<LootTable> { return this.http.post<LootTable>(this.base, t); }
  update(t: LootTable): Observable<LootTable> { return this.http.put<LootTable>(`${this.base}/${t.lootTableId}`, t); }
  delete(lootTableId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${lootTableId}`); }
}
