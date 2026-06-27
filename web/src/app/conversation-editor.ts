import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ConversationSet, ConversationKeyword, ConversationService, emptySet, emptyKeyword,
} from './conversation.service';
import { FactionService } from './faction.service';

/**
 * The web Conversation Editor (M2.4). A set is a named list of keywords; each keyword has a response,
 * a mode (passive = always heard / active = only mid-conversation), flags, an optional faction gate,
 * and keywords it unlocks. Faction gate is by id + standing (live once factions are in DB at 2.6).
 */
@Component({
  selector: 'app-conversation-editor',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <aside>
        <div class="head">
          <h2>Conversations</h2>
          <button (click)="newSet()">+ New</button>
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <ul>
          @for (s of sets(); track s.setId) {
            <li [class.active]="model?.setId === s.setId && !isNew" (click)="select(s)">
              <span class="id">{{ s.setId }}</span>
              <span class="muted">{{ s.keywords.length }} keyword(s)</span>
            </li>
          } @empty {
            <li class="muted">No sets — click “New”.</li>
          }
        </ul>
      </aside>

      <main>
        @if (model) {
          <h1>{{ isNew ? 'New set' : model.setId }}</h1>

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
            </section>
          } @empty {
            <p class="muted">No keywords yet.</p>
          }

          <div class="actions">
            <button class="primary" (click)="save()" [disabled]="isNew && !model.setId.trim()">Save</button>
            @if (!isNew) { <button class="danger" (click)="remove()">Delete</button> }
          </div>
        } @else {
          <p class="muted">Select a conversation set or create a new one.</p>
        }
      </main>
    </div>
  `,
  styles: [`
    .wrap { display: flex; gap: 1rem; }
    aside { width: 220px; flex-shrink: 0; border-right: 1px solid #eee; padding-right: 1rem; }
    .head, .kwhead { display: flex; justify-content: space-between; align-items: center; }
    aside ul { list-style: none; padding: 0; margin: 0.5rem 0 0; }
    aside li { padding: 0.4rem 0.5rem; cursor: pointer; border-radius: 4px; display: flex; flex-direction: column; }
    aside li:hover { background: #f4f4f4; }
    aside li.active { background: #e8f0fe; }
    .id { font-weight: 600; font-size: 0.9rem; }
    main { flex: 1; }
    h1 { font-size: 1.2rem; }
    section { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 0.75rem; }
    section.kw { background: #fcfcfc; }
    h3 { margin: 0 0 0.5rem; font-size: 0.95rem; color: #444; }
    label { display: block; margin: 0.35rem 0; font-size: 0.82rem; color: #555; }
    input, textarea, select { padding: 0.35rem; box-sizing: border-box; }
    label input, label textarea { width: 100%; }
    .kwtop { display: flex; gap: 0.4rem; align-items: center; margin-bottom: 0.3rem; }
    .kwword { flex: 1; font-weight: 600; }
    .flags { display: flex; gap: 1rem; flex-wrap: wrap; }
    .check { display: flex; gap: 0.3rem; align-items: center; }
    .check input { width: auto; }
    .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0 0.6rem; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
    button { cursor: pointer; padding: 0.4rem 0.8rem; }
    button.small { padding: 0.2rem 0.5rem; }
    .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
    .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
    .muted { color: #999; }
    .error { color: #c00; font-size: 0.85rem; }
  `]
})
export class ConversationEditor implements OnInit {
  private readonly api = inject(ConversationService);
  private readonly factionApi = inject(FactionService);

  readonly sets = signal<ConversationSet[]>([]);
  readonly factionIds = signal<string[]>([]);
  readonly standingNames = signal<string[]>([]);
  readonly error = signal<string | null>(null);
  model: ConversationSet | null = null;
  isNew = false;

  ngOnInit(): void {
    this.reload();
    this.factionApi.getAll().subscribe({ next: rows => this.factionIds.set(rows.map(r => r.factionId)) });
    this.factionApi.getThresholds().subscribe({ next: rows => this.standingNames.set(rows.map(r => r.name)) });
  }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.sets.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newSet(): void { this.model = emptySet(); this.isNew = true; }

  select(s: ConversationSet): void {
    // Deep-ish clone so edits don't mutate the list row until saved.
    this.model = { ...s, keywords: s.keywords.map(k => ({ ...k, unlocks: [...k.unlocks] })) };
    this.isNew = false;
  }

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
      next: saved => { this.isNew = false; this.model = saved; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  remove(): void {
    if (!this.model || this.isNew) return;
    this.api.delete(this.model.setId).subscribe({
      next: () => { this.model = null; this.reload(); },
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
