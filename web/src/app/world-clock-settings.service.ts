import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

/** The single shared day-length/lunar-cycle/fog config (5.12 follow-up). Always id 1 — one row, not a list. */
export interface WorldClockSettings {
  id: number;
  dayLengthMinutes: number;
  lunarCycleDays: number;
  fogStartDistance: number;
  fogEndDistance: number;
  updatedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class WorldClockSettingsService {
  private readonly base = `${environment.apiBase}/api/world-clock-settings`;
  private readonly http = inject(HttpClient);

  get(): Observable<WorldClockSettings> { return this.http.get<WorldClockSettings>(this.base); }
  update(settings: WorldClockSettings): Observable<WorldClockSettings> {
    return this.http.put<WorldClockSettings>(this.base, settings);
  }
}
