import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../environments/environment';

export interface AuthResponse { username: string; }

/**
 * Web-admin auth (5.11) — session is a JWT in an HttpOnly cookie the API sets, so this service
 * never sees or stores the token itself. `username` is a signal `app.ts` reads to decide whether
 * to show the login screen or the editor shell; `withCredentialsInterceptor` (auth.interceptor.ts)
 * is what actually attaches the cookie to every request.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly base = `${environment.apiBase}/api/auth`;
  private readonly http = inject(HttpClient);

  /** null = logged out / not yet checked; a username = logged in. */
  readonly username = signal<string | null>(null);
  /** Becomes true once the initial session check (checkSession) resolves, success or failure. */
  readonly ready = signal(false);

  checkSession(): void {
    this.http.get<AuthResponse>(`${this.base}/me`).subscribe({
      next: (r) => { this.username.set(r.username); this.ready.set(true); },
      error: () => { this.username.set(null); this.ready.set(true); },
    });
  }

  login(username: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/login`, { username, password })
      .pipe(tap((r) => this.username.set(r.username)));
  }

  register(username: string, password: string, inviteCode: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/register`, { username, password, inviteCode })
      .pipe(tap((r) => this.username.set(r.username)));
  }

  logout(): void {
    this.http.post(`${this.base}/logout`, {}).subscribe(() => this.username.set(null));
  }
}
