import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GridColumn } from './shared/content-grid';

/** Mirrors the API's VendorDto (M2.3) — a vendor plus its ordered item ids. */
export interface Vendor {
  vendorId: string;
  displayName: string;
  itemIds: string[];
}

export function emptyVendor(): Vendor {
  return { vendorId: '', displayName: '', itemIds: [] };
}

/** Grid columns for the Vendor index (2.1.1 AF5). */
export const VENDOR_GRID_COLUMNS: GridColumn<Vendor>[] = [
  { header: 'ID', accessor: v => v.vendorId },
  { header: 'Name', accessor: v => v.displayName },
  { header: 'Items', accessor: v => v.itemIds.length },
];
export const VENDOR_SEARCH_FIELDS: (keyof Vendor)[] = ['vendorId', 'displayName'];

@Injectable({ providedIn: 'root' })
export class VendorService {
  private readonly base = `${environment.apiBase}/api/vendors`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<Vendor[]> { return this.http.get<Vendor[]>(this.base); }
  create(v: Vendor): Observable<Vendor> { return this.http.post<Vendor>(this.base, v); }
  update(v: Vendor): Observable<Vendor> { return this.http.put<Vendor>(`${this.base}/${v.vendorId}`, v); }
  delete(vendorId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${vendorId}`); }
}
