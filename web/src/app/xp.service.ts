import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

/** One row of the shared XP curve (M2.7) — XP to advance from `level` to `level+1`. */
export interface XpLevel { level: number; xpToNext: number; }

@Injectable({ providedIn: 'root' })
export class XpService {
  private readonly base = `${environment.apiBase}/api/xp-levels`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<XpLevel[]> { return this.http.get<XpLevel[]>(this.base); }
  replace(rows: XpLevel[]): Observable<XpLevel[]> { return this.http.put<XpLevel[]>(this.base, rows); }
}
