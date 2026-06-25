import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Mirrors the `content_ping` row shape returned by the .NET API (2.1 smoke type). */
export interface ContentPing {
  id: number;
  label: string;
  updatedAt: string;
}

/**
 * HTTP client for the content_ping CRUD endpoints on the .NET API. This is the reference
 * shape every real content-type service (items = 2.2) copies. The API base points at the
 * api project's http launch profile (see api/Properties/launchSettings.json).
 */
@Injectable({ providedIn: 'root' })
export class ContentPingService {
  // Local dev: the .NET API's http profile. Swap to a config/environment file when hosted.
  private readonly base = 'http://localhost:5144/api/content-ping';
  private readonly http = inject(HttpClient);

  getAll(): Observable<ContentPing[]> {
    return this.http.get<ContentPing[]>(this.base);
  }

  create(label: string): Observable<ContentPing> {
    return this.http.post<ContentPing>(this.base, { label });
  }

  update(id: number, label: string): Observable<ContentPing> {
    return this.http.put<ContentPing>(`${this.base}/${id}`, { label });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
