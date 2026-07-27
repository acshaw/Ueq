import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { finalize } from 'rxjs';
import { LoadingService } from './loading.service';

/**
 * Bumps `LoadingService`'s counter around every HTTP request so the app shell can render one global
 * progress bar, without every editor having to wrap its own `subscribe()` calls in a spinner.
 * `finalize` (not `tap`) so the counter still decrements on error or unsubscribe, not just success.
 */
export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const loading = inject(LoadingService);
  loading.start();
  return next(req).pipe(finalize(() => loading.stop()));
};
