import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Mirrors the API's VendorDto (M2.3) — a vendor plus its ordered item ids. */
export interface Vendor {
  vendorId: string;
  displayName: string;
  itemIds: string[];
}

export function emptyVendor(): Vendor {
  return { vendorId: '', displayName: '', itemIds: [] };
}

@Injectable({ providedIn: 'root' })
export class VendorService {
  private readonly base = 'http://localhost:5144/api/vendors';
  private readonly http = inject(HttpClient);

  getAll(): Observable<Vendor[]> { return this.http.get<Vendor[]>(this.base); }
  create(v: Vendor): Observable<Vendor> { return this.http.post<Vendor>(this.base, v); }
  update(v: Vendor): Observable<Vendor> { return this.http.put<Vendor>(`${this.base}/${v.vendorId}`, v); }
  delete(vendorId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${vendorId}`); }
}
