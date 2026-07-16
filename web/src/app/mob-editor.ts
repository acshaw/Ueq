import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Mob, MobService, emptyMob } from './mob.service';
import { ConversationService } from './conversation.service';
import { VendorService } from './vendor.service';
import { FactionService } from './faction.service';
import { LootService } from './loot.service';

/**
 * The web Mob Editor (M2.5) — the cluster's testable milestone. Authors a mob fully in the browser;
 * a scene SpawnPoint resolves it by id. References other content by id: conversation + vendor are
 * dropdowns (already in the DB); faction + loot are text ids for now (dropdowns light up at 2.6 / 2.7).
 * prefab_address names a registered Mirror spawnable prefab (most mobs use "Enemy").
 */
@Component({
  selector: 'app-mob-editor',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <aside>
        <div class="head">
          <h2>Mobs</h2>
          <button (click)="newMob()">+ New</button>
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <ul>
          @for (m of mobs(); track m.mobId) {
            <li [class.active]="model?.mobId === m.mobId && !isNew" (click)="select(m)">
              <span class="id">{{ m.mobId }}</span>
              <span class="muted">lvl {{ m.mobLevel }} · {{ m.displayName }}</span>
            </li>
          } @empty {
            <li class="muted">No mobs — click “New”.</li>
          }
        </ul>
      </aside>

      <main>
        @if (model) {
          <h1>{{ isNew ? 'New mob' : model.mobId }}</h1>

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
              <label>Weapon category
                <select [(ngModel)]="model.weaponCategory" name="weaponCategory">
                  <option [ngValue]="0">Might</option>
                  <option [ngValue]="1">Finesse</option>
                </select>
              </label>
              <label>Weapon skill <input type="number" [(ngModel)]="model.weaponSkill" name="weaponSkill" /></label>
              <label>Attack parryable
                <select [(ngModel)]="model.attackIsParryable" name="attackIsParryable">
                  <option [ngValue]="true">Yes (weapon-style)</option>
                  <option [ngValue]="false">No (beast/unarmed-style)</option>
                </select>
              </label>
              <label>Avoidance Agility <input type="number" step="0.1" [(ngModel)]="model.avoidanceAgility" name="avoidanceAgility" /></label>
              <label>Avoidance Dexterity <input type="number" step="0.1" [(ngModel)]="model.avoidanceDexterity" name="avoidanceDexterity" /></label>
            </div>
            <p class="soon">Hit-tier weighted table (design doc §2.2) — this mob's own weights, not derived from level.</p>
            <div class="grid">
              <label>Miss <input type="number" step="0.1" [(ngModel)]="model.tierMiss" name="tierMiss" /></label>
              <label>Glancing <input type="number" step="0.1" [(ngModel)]="model.tierGlancing" name="tierGlancing" /></label>
              <label>Hit <input type="number" step="0.1" [(ngModel)]="model.tierHit" name="tierHit" /></label>
              <label>Solid Hit <input type="number" step="0.1" [(ngModel)]="model.tierSolid" name="tierSolid" /></label>
              <label>Good Hit <input type="number" step="0.1" [(ngModel)]="model.tierGood" name="tierGood" /></label>
              <label>Critical <input type="number" step="0.1" [(ngModel)]="model.tierCritical" name="tierCritical" /></label>
              <label>Crippling <input type="number" step="0.1" [(ngModel)]="model.tierCrippling" name="tierCrippling" /></label>
            </div>
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

          <div class="actions">
            <button class="primary" (click)="save()" [disabled]="isNew && !model.mobId.trim()">Save</button>
            @if (!isNew) { <button class="danger" (click)="remove()">Delete</button> }
          </div>
        } @else {
          <p class="muted">Select a mob or create a new one.</p>
        }
      </main>
    </div>
  `,
  styles: [`
    .wrap { display: flex; gap: 1rem; }
    aside { width: 240px; flex-shrink: 0; border-right: 1px solid #eee; padding-right: 1rem; }
    .head { display: flex; justify-content: space-between; align-items: center; }
    aside ul { list-style: none; padding: 0; margin: 0.5rem 0 0; }
    aside li { padding: 0.4rem 0.5rem; cursor: pointer; border-radius: 4px; display: flex; flex-direction: column; }
    aside li:hover { background: #f4f4f4; }
    aside li.active { background: #e8f0fe; }
    .id { font-weight: 600; font-size: 0.9rem; }
    main { flex: 1; }
    h1 { font-size: 1.2rem; }
    section { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 0.75rem; }
    h3 { margin: 0 0 0.5rem; font-size: 0.95rem; color: #444; }
    label { display: block; margin: 0.35rem 0; font-size: 0.82rem; color: #555; }
    input, select { width: 100%; padding: 0.35rem; box-sizing: border-box; }
    .pair { display: flex; gap: 0.3rem; }
    .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0 0.75rem; }
    .soon { color: #bbb; font-size: 0.75rem; }
    .rowhead { display: flex; justify-content: space-between; align-items: center; }
    .rowhead span { display: flex; gap: 0.3rem; }
    .hitrow { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .hitrow select { flex: 1; }
    .hitrow .w { display: flex; flex-direction: column; width: 90px; margin: 0; }
    .hitrow .w input { width: 100%; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    button.small { padding: 0.2rem 0.5rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
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

  newMob(): void { this.model = emptyMob(); this.isNew = true; }
  select(m: Mob): void {
    // Deep-clone the faction-hit list so edits don't mutate the list row until saved.
    this.model = { ...m, factionHits: (m.factionHits ?? []).map(h => ({ ...h })) };
    this.isNew = false;
  }

  addFactionHit(): void { this.model?.factionHits.push({ factionId: '', delta: 0 }); }
  addOwnFactionHit(): void {
    if (this.model?.factionId) this.model.factionHits.push({ factionId: this.model.factionId, delta: -10 });
  }

  save(): void {
    if (!this.model) return;
    const call = this.isNew ? this.api.create(this.model) : this.api.update(this.model);
    call.subscribe({
      next: saved => { this.isNew = false; this.model = saved; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model || this.isNew) return;
    this.api.delete(this.model.mobId).subscribe({
      next: () => { this.model = null; this.reload(); },
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
