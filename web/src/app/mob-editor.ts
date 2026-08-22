import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Mob, MobService, emptyMob, MOB_GRID_COLUMNS, MOB_SEARCH_FIELDS } from './mob.service';
import { ConversationService } from './conversation.service';
import { VendorService } from './vendor.service';
import { FactionService } from './faction.service';
import { LootService } from './loot.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Mob Editor (M2.5, retrofitted onto the 2.1.1 admin framework). Authors a mob fully in the
 * browser; a scene SpawnPoint resolves it by id. References other content by id: conversation + vendor
 * are dropdowns (already in the DB); faction + loot are dropdowns too (2.6/2.7 landed since this was
 * first written). prefab_address names a registered Mirror spawnable prefab (most mobs use "Enemy").
 */
@Component({
  selector: 'app-mob-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Mobs</h1>
      <button class="primary" (click)="newMob()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="mobs()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New mob' : (model?.mobId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.mobId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <div class="grid">
            <label>mob_id <input [(ngModel)]="model.mobId" name="mobId" [disabled]="!isNew" placeholder="Giant Rat" /></label>
            <label>Display name <input [(ngModel)]="model.displayName" name="displayName" /></label>
            <label>Level <input type="number" [(ngModel)]="model.mobLevel" name="mobLevel" /></label>
            <label>Prefab (spawnable) <input [(ngModel)]="model.prefabAddress" name="prefabAddress" placeholder="Enemy" /></label>
          </div>
        </section>

        <section>
          <h3>Combat</h3>
          <div class="grid">
            <label>Max health <input type="number" [(ngModel)]="model.maxHealth" name="maxHealth" /></label>
            <label>Attack damage <input type="number" [(ngModel)]="model.attackDamage" name="attackDamage" /></label>
            <label>Attack interval (s) <input type="number" step="0.1" [(ngModel)]="model.attackInterval" name="attackInterval" /></label>
            <label>Attack range <input type="number" step="0.1" [(ngModel)]="model.attackRange" name="attackRange" /></label>
          </div>
        </section>

        <section>
          <h3>Movement</h3>
          <div class="grid">
            <label>Type
              <select [(ngModel)]="model.movementType" name="movementType">
                <option [ngValue]="0">Stationary</option>
                <option [ngValue]="1">Wander</option>
              </select>
            </label>
            <label>Move speed <input type="number" step="0.1" [(ngModel)]="model.moveSpeed" name="moveSpeed" /></label>
            <label>Wander radius <input type="number" step="0.1" [(ngModel)]="model.wanderRadius" name="wanderRadius" /></label>
            <label>Pause min/max
              <span class="pair">
                <input type="number" step="0.1" [(ngModel)]="model.wanderPauseMin" name="wanderPauseMin" />
                <input type="number" step="0.1" [(ngModel)]="model.wanderPauseMax" name="wanderPauseMax" />
              </span>
            </label>
          </div>
        </section>

        <section>
          <h3>AI &amp; Faction</h3>
          <div class="grid">
            <label>Perception radius <input type="number" step="0.1" [(ngModel)]="model.perceptionRadius" name="perceptionRadius" /></label>
            <label>Base aggro threat <input type="number" [(ngModel)]="model.baseAggroThreat" name="baseAggroThreat" /></label>
            <label>Social aggro (5.4)
              <select [(ngModel)]="model.socialAggroEnabled" name="socialAggroEnabled">
                <option [ngValue]="false">Off (solitary)</option>
                <option [ngValue]="true">On (calls nearby allies into the fight)</option>
              </select>
            </label>
            <label>Social aggro radius <input type="number" step="0.1" [(ngModel)]="model.socialAggroRadius" name="socialAggroRadius" [disabled]="!model.socialAggroEnabled" /></label>
            <label>Faction
              <select [(ngModel)]="model.factionId" name="factionId">
                <option [ngValue]="null">(none)</option>
                @for (id of factionIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
            </label>
            <label>Aggro ≤ standing <input [(ngModel)]="model.aggroMaxStanding" name="aggroMaxStanding" /></label>
            <label>Warn ≤ standing <input [(ngModel)]="model.warningMaxStanding" name="warningMaxStanding" /></label>
          </div>
        </section>

        <section>
          <h3>Links &amp; Rewards</h3>
          <div class="grid">
            <label>Conversation set
              <select [(ngModel)]="model.conversationSetId" name="conversationSetId">
                <option [ngValue]="null">(none)</option>
                @for (id of conversationIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
            </label>
            <label>Vendor
              <select [(ngModel)]="model.vendorId" name="vendorId">
                <option [ngValue]="null">(none)</option>
                @for (id of vendorIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
            </label>
            <label>Vendor open keyword <input [(ngModel)]="model.vendorOpenKeyword" name="vendorOpenKeyword" /></label>
            <label>Loot table
              <select [(ngModel)]="model.lootTableId" name="lootTableId">
                <option [ngValue]="null">(none)</option>
                @for (id of lootTableIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
            </label>
            <label>XP reward <input type="number" [(ngModel)]="model.xpReward" name="xpReward" /></label>
          </div>
        </section>

        <section>
          <h3>Combat Pipeline (5.1)</h3>
          <div class="grid">
            <label>Weapon category <span class="soon">(vestigial as of 5.1.5 — no longer read by the resolver)</span>
              <select [(ngModel)]="model.weaponCategory" name="weaponCategory">
                <option [ngValue]="0">Might</option>
                <option [ngValue]="1">Finesse</option>
              </select>
            </label>
            <label>Attack parryable
              <select [(ngModel)]="model.attackIsParryable" name="attackIsParryable">
                <option [ngValue]="true">Yes (weapon-style)</option>
                <option [ngValue]="false">No (beast/unarmed-style)</option>
              </select>
            </label>
            <label>Avoidance Dodge <input type="number" step="0.1" [(ngModel)]="model.avoidanceDodge" name="avoidanceDodge" /></label>
            <label>Avoidance Parry <input type="number" step="0.1" [(ngModel)]="model.avoidanceParry" name="avoidanceParry" /></label>
            <label>Avoidance Riposte <input type="number" step="0.1" [(ngModel)]="model.avoidanceRiposte" name="avoidanceRiposte" /></label>
            <label>ATK <input type="number" step="0.1" [(ngModel)]="model.atk" name="atk" /></label>
            <label>AC <input type="number" step="0.1" [(ngModel)]="model.ac" name="ac" /></label>
          </div>
          <p class="soon">ATK (5.1.5) — this mob's hit-tier potency, authored directly as one number (mobs have no
            stats to derive it from). Feeds the same shared ATK curve a player's ATK does — tune it in the Combat
            Simulator (left nav) before saving here.</p>
          <p class="soon">AC (Mitigation, 2026-08-21) — this mob's sole mitigation lever, authored directly as one
            number. Feeds the same shared diminishing-returns mitigation curve a player's AC does.</p>
        </section>

        <section>
          <div class="rowhead"><h3>Faction hits on kill</h3>
            <span>
              <button (click)="addOwnFactionHit()" [disabled]="!model.factionId">+ Own faction</button>
              <button (click)="addFactionHit()">+ Hit</button>
            </span>
          </div>
          <p class="soon">Applied to the killing player. Negative = standing worsens, positive = improves.</p>
          @for (h of model.factionHits; track $index; let i = $index) {
            <div class="hitrow">
              <select [(ngModel)]="h.factionId" [name]="'fh'+i">
                <option [ngValue]="''">(choose faction)</option>
                @for (id of factionIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
              </select>
              <label class="w">delta <input type="number" [(ngModel)]="h.delta" [name]="'fhd'+i" /></label>
              <button class="small danger" (click)="model.factionHits.splice(i,1)">✕</button>
            </div>
          } @empty { <p class="muted">No faction consequence on kill.</p> }
        </section>
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .pair { display: flex; gap: 0.3rem; }
    .soon { color: #bbb; font-size: 0.75rem; }
    .hitrow { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .hitrow select { flex: 1; }
    .hitrow .w { display: flex; flex-direction: column; width: 90px; margin: 0; }
    .hitrow .w input { width: 100%; }
  `],
})
export class MobEditor implements OnInit {
  private readonly api = inject(MobService);
  private readonly convApi = inject(ConversationService);
  private readonly vendorApi = inject(VendorService);
  private readonly factionApi = inject(FactionService);
  private readonly lootApi = inject(LootService);

  readonly mobs = signal<Mob[]>([]);
  readonly conversationIds = signal<string[]>([]);
  readonly vendorIds = signal<string[]>([]);
  readonly factionIds = signal<string[]>([]);
  readonly lootTableIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: Mob | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = MOB_GRID_COLUMNS;
  readonly searchFields = MOB_SEARCH_FIELDS;

  ngOnInit(): void {
    this.reload();
    this.convApi.getAll().subscribe({ next: rows => this.conversationIds.set(rows.map(r => r.setId)) });
    this.vendorApi.getAll().subscribe({ next: rows => this.vendorIds.set(rows.map(r => r.vendorId)) });
    this.factionApi.getAll().subscribe({ next: rows => this.factionIds.set(rows.map(r => r.factionId)) });
    this.lootApi.getAll().subscribe({ next: rows => this.lootTableIds.set(rows.map(r => r.lootTableId)) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.mobs.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newMob(): void { this.model = emptyMob(); this.isNew = true; this.modalOpen = true; }
  select(m: Mob): void {
    // Deep-clone the faction-hit list so edits don't mutate the list row until saved.
    this.model = { ...m, factionHits: (m.factionHits ?? []).map(h => ({ ...h })) };
    this.isNew = false;
    this.modalOpen = true;
  }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

  addFactionHit(): void { this.model?.factionHits.push({ factionId: '', delta: 0 }); }
  addOwnFactionHit(): void {
    if (this.model?.factionId) this.model.factionHits.push({ factionId: this.model.factionId, delta: -10 });
  }

  save(): void {
    if (!this.model) return;
    const call = this.isNew ? this.api.create(this.model) : this.api.update(this.model);
    call.subscribe({
      next: saved => { this.isNew = false; this.model = saved; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model || this.isNew) return;
    this.api.delete(this.model.mobId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A mob with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
