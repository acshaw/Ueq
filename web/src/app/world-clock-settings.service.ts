import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

/**
 * The single shared day-length/lunar-cycle/fog config (5.12 follow-up), extended (8.1) with the
 * calendar's display names and a read-only view of Age. Always id 1 — one row, not a list.
 * Age/ageStartingYearDisplay are display-only here — `/set-age` in-game is the sole live-mutation path
 * (see the API controller's doc comment for why).
 */
export interface WorldClockSettings {
  id: number;
  dayLengthMinutes: number;
  lunarCycleDays: number;
  fogStartDistance: number;
  fogEndDistance: number;
  updatedAt?: string;
  age: number;
  ageStartingYearDisplay: number;
  dayNames: string[];
  monthNames: string[];
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
