import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Two jobs (5.11): (1) attach the session cookie to every request — `withCredentials` has to be
 * set per-request, not globally, and touching all 12 service files again wasn't worth it; (2) if
 * any request comes back 401 (session expired / never logged in), flip the shared `username`
 * signal to null so `app.ts` reactively falls back to the login screen instead of a dead editor.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  return next(req.clone({ withCredentials: true })).pipe(
    catchError((err) => {
      if (err?.status === 401) auth.username.set(null);
      return throwError(() => err);
    }),
  );
};
