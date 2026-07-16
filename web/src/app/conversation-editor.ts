import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ConversationSet, ConversationKeyword, ConversationService, emptySet, emptyKeyword,
  CONVERSATION_GRID_COLUMNS, CONVERSATION_SEARCH_FIELDS,
} from './conversation.service';
import { FactionService } from './faction.service';
import { ItemService } from './item.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Conversation Editor (M2.4, retrofitted onto the 2.1.1 admin framework). A set is a named
 * list of keywords; each keyword has a response, a mode (passive = always heard / active = only
 * mid-conversation), flags, an optional faction gate, keywords it unlocks, and an optional quest
 * transaction (turn-in requirements → reward). The most structurally complex editor — retrofitted
 * last per AF8 — so its nested per-keyword markup is left entirely as-is, just moved into the modal.
 */
@Component({
  selector: 'app-conversation-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Conversations</h1>
      <button class="primary" (click)="newSet()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="sets()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New set' : (model?.setId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.setId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Identity</h3>
          <label>set_id <input [(ngModel)]="model.setId" name="setId" [disabled]="!isNew" placeholder="GuardKeywords" /></label>
          <label>Display name <input [(ngModel)]="model.displayName" name="displayName" /></label>
        </section>

        <div class="kwhead">
          <h3>Keywords</h3>
          <button (click)="addKeyword()">+ Add keyword</button>
        </div>

        @for (kw of model.keywords; track $index; let i = $index) {
          <section class="kw">
            <div class="kwtop">
              <input class="kwword" [(ngModel)]="kw.keyword" [name]="'kw'+i" placeholder="keyword" />
              <select [(ngModel)]="kw.mode" [name]="'mode'+i">
                <option [ngValue]="0">Passive</option>
                <option [ngValue]="1">Active</option>
              </select>
              <button class="small" (click)="moveKeyword(i,-1)" [disabled]="i===0">↑</button>
              <button class="small" (click)="moveKeyword(i,1)" [disabled]="i===model.keywords.length-1">↓</button>
              <button class="small danger" (click)="removeKeyword(i)">✕</button>
            </div>
            <label>Response <textarea [(ngModel)]="kw.response" [name]="'resp'+i" rows="2"
                     placeholder="Supports <name> <race> <class> <gender>"></textarea></label>
            <div class="flags">
              <label class="check"><input type="checkbox" [(ngModel)]="kw.isOpener" [name]="'op'+i" /> Opener (hail)</label>
              <label class="check"><input type="checkbox" [(ngModel)]="kw.endsConversation" [name]="'end'+i" /> Ends conversation</label>
              <label class="check"><input type="checkbox" [(ngModel)]="kw.requiresUnlock" [name]="'ru'+i" /> Requires unlock</label>
            </div>
            <div class="grid">
              <label>Unlocks (comma-separated)
                <input [ngModel]="unlocksText(kw)" (ngModelChange)="setUnlocks(kw, $event)" [name]="'unl'+i" placeholder="danger, secret" /></label>
              <label>Required faction
                <select [(ngModel)]="kw.requiredFactionId" [name]="'fac'+i">
                  <option [ngValue]="null">(none)</option>
                  @for (id of factionIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
                </select></label>
              <label>Required standing
                <select [(ngModel)]="kw.requiredStanding" [name]="'st'+i">
                  <option [ngValue]="null">(any)</option>
                  @for (s of standingNames(); track s) { <option [ngValue]="s">{{ s }}</option> }
                </select></label>
            </div>

            <details class="tx">
              <summary>Quest transaction (turn-in → reward) @if (hasTx(kw)) { <span class="badge">active</span> }</summary>
              <div class="txcols">
                <div class="txcol">
                  <h4>Requirements (consumed on turn-in)</h4>
                  <label>Coin (copper) <input type="number" min="0" [(ngModel)]="kw.requiredCopper" [name]="'rqc'+i" /></label>
                  @for (ri of kw.requiredItems; track $index; let ai = $index) {
                    <div class="row">
                      <select [(ngModel)]="ri.itemId" [name]="'rqi'+i+'_'+ai">
                        <option [ngValue]="''">(item)</option>
                        @for (id of itemIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
                      </select>
                      <input type="number" min="1" [(ngModel)]="ri.quantity" [name]="'rqiq'+i+'_'+ai" />
                      <button class="small danger" (click)="removeAt(kw.requiredItems, ai)">✕</button>
                    </div>
                  }
                  <button class="small" (click)="kw.requiredItems.push({ itemId: '', quantity: 1 })">+ Required item</button>
                </div>

                <div class="txcol">
                  <h4>Reward (granted)</h4>
                  <label>XP <input type="number" min="0" [(ngModel)]="kw.rewardXp" [name]="'rwx'+i" /></label>
                  <label>Coin (copper) <input type="number" min="0" [(ngModel)]="kw.rewardCopper" [name]="'rwc'+i" /></label>
                  @for (rw of kw.rewardItems; track $index; let bi = $index) {
                    <div class="row">
                      <select [(ngModel)]="rw.itemId" [name]="'rwi'+i+'_'+bi">
                        <option [ngValue]="''">(item)</option>
                        @for (id of itemIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
                      </select>
                      <input type="number" min="1" [(ngModel)]="rw.quantity" [name]="'rwiq'+i+'_'+bi" />
                      <button class="small danger" (click)="removeAt(kw.rewardItems, bi)">✕</button>
                    </div>
                  }
                  <button class="small" (click)="kw.rewardItems.push({ itemId: '', quantity: 1 })">+ Reward item</button>
                  @for (fh of kw.factionHits; track $index; let ci = $index) {
                    <div class="row">
                      <select [(ngModel)]="fh.factionId" [name]="'fh'+i+'_'+ci">
                        <option [ngValue]="''">(faction)</option>
                        @for (id of factionIds(); track id) { <option [ngValue]="id">{{ id }}</option> }
                      </select>
                      <input type="number" [(ngModel)]="fh.delta" [name]="'fhd'+i+'_'+ci" placeholder="±" />
                      <button class="small danger" (click)="removeAt(kw.factionHits, ci)">✕</button>
                    </div>
                  }
                  <button class="small" (click)="kw.factionHits.push({ factionId: '', delta: 0 })">+ Faction hit</button>
                </div>
              </div>
            </details>
          </section>
        } @empty {
          <p class="muted">No keywords yet.</p>
        }
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES, `
    .kwhead { display: flex; justify-content: space-between; align-items: center; }
    section.kw { background: #fcfcfc; }
    .kwtop { display: flex; gap: 0.4rem; align-items: center; margin-bottom: 0.3rem; }
    .kwword { flex: 1; font-weight: 600; }
    .flags { display: flex; gap: 1rem; flex-wrap: wrap; }
    .grid { grid-template-columns: repeat(3, 1fr); }
    .tx { margin-top: 0.5rem; border-top: 1px dashed #ddd; padding-top: 0.4rem; }
    .tx summary { cursor: pointer; font-size: 0.82rem; color: #666; }
    .tx .badge { background: #1a73e8; color: #fff; border-radius: 3px; padding: 0 0.3rem; font-size: 0.7rem; }
    .txcols { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; margin-top: 0.4rem; }
    .txcol h4 { margin: 0 0 0.3rem; font-size: 0.82rem; color: #444; }
    .txcol .row { display: flex; gap: 0.3rem; align-items: center; margin: 0.2rem 0; }
    .txcol .row select { flex: 1; }
    .txcol .row input { width: 56px; }
  `],
})
export class ConversationEditor implements OnInit {
  private readonly api = inject(ConversationService);
  private readonly factionApi = inject(FactionService);
  private readonly itemApi = inject(ItemService);

  readonly sets = signal<ConversationSet[]>([]);
  readonly factionIds = signal<string[]>([]);
  readonly standingNames = signal<string[]>([]);
  readonly itemIds = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: ConversationSet | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = CONVERSATION_GRID_COLUMNS;
  readonly searchFields = CONVERSATION_SEARCH_FIELDS;

  ngOnInit(): void {
    this.reload();
    this.factionApi.getAll().subscribe({ next: rows => this.factionIds.set(rows.map(r => r.factionId)) });
    this.factionApi.getThresholds().subscribe({ next: rows => this.standingNames.set(rows.map(r => r.name)) });
    this.itemApi.getAll().subscribe({ next: rows => this.itemIds.set(rows.map(r => r.itemId)) });
  }

  hasTx(kw: ConversationKeyword): boolean {
    return kw.rewardXp > 0 || kw.rewardCopper > 0 || kw.requiredCopper > 0 ||
      kw.requiredItems.length > 0 || kw.rewardItems.length > 0 || kw.factionHits.length > 0;
  }

  removeAt(arr: unknown[], i: number): void { arr.splice(i, 1); }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.sets.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newSet(): void { this.model = emptySet(); this.isNew = true; this.modalOpen = true; }

  select(s: ConversationSet): void {
    // Deep-ish clone so edits don't mutate the list row until saved.
    this.model = {
      ...s,
      keywords: s.keywords.map(k => ({
        ...k,
        unlocks: [...k.unlocks],
        requiredItems: (k.requiredItems ?? []).map(x => ({ ...x })),
        rewardItems: (k.rewardItems ?? []).map(x => ({ ...x })),
        factionHits: (k.factionHits ?? []).map(x => ({ ...x })),
      })),
    };
    this.isNew = false;
    this.modalOpen = true;
  }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

  addKeyword(): void { this.model?.keywords.push(emptyKeyword()); }
  removeKeyword(i: number): void { this.model?.keywords.splice(i, 1); }

  moveKeyword(i: number, dir: number): void {
    if (!this.model) return;
    const j = i + dir;
    if (j < 0 || j >= this.model.keywords.length) return;
    const a = this.model.keywords;
    [a[i], a[j]] = [a[j], a[i]];
  }

  unlocksText(kw: ConversationKeyword): string { return kw.unlocks.join(', '); }
  setUnlocks(kw: ConversationKeyword, text: string): void {
    kw.unlocks = text.split(',').map(s => s.trim()).filter(s => s.length > 0);
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
    this.api.delete(this.model.setId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'A conversation set with that id already exists.';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
