import { Injectable, signal } from '@angular/core';

/**
 * Counts in-flight HTTP requests (fed by `loadingInterceptor`) so the app shell can show one global
 * loading indicator instead of every editor wiring its own spinner around every `subscribe()` call.
 * A counter, not a boolean, because several requests can overlap (e.g. a grid reload firing while a
 * modal's save is still in flight) — only the last one to finish should turn the indicator off.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly count = signal(0);
  readonly active = signal(false);

  start(): void {
    this.count.update(n => n + 1);
    this.active.set(true);
  }

  stop(): void {
    this.count.update(n => Math.max(0, n - 1));
    if (this.count() === 0) this.active.set(false);
  }
}
