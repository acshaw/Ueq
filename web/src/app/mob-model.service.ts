import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

/**
 * Mirrors the API's MobModel (2026-09-19) — a read-only lookup of the model ids known to Unity's
 * MobModelCatalog, kept in sync by the Editor tool Tools/Character/Sync Mob Model Catalog to Database.
 * Feeds the Mob Editor's Body Model dropdown; there is no create/update/delete from the web.
 */
export interface MobModel { modelId: string; }

@Injectable({ providedIn: 'root' })
export class MobModelService {
  private readonly base = `${environment.apiBase}/api/mob-models`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<MobModel[]> { return this.http.get<MobModel[]>(this.base); }
}
