import { Component, signal } from '@angular/core';
import { ItemEditor } from './item-editor';
import { VendorEditor } from './vendor-editor';
import { ConversationEditor } from './conversation-editor';
import { MobEditor } from './mob-editor';
import { FactionEditor } from './faction-editor';
import { LootEditor } from './loot-editor';
import { XpEditor } from './xp-editor';
import { SpawnEditor } from './spawn-editor';

/**
 * Shell for the Ueq content editors. A simple left-nav switches between the per-type editors
 * (Items, Vendors, Conversations, … more as the M2 cluster lands). One editor visible at a time.
 */
@Component({
  selector: 'app-root',
  imports: [ItemEditor, VendorEditor, ConversationEditor, MobEditor, FactionEditor, LootEditor, XpEditor, SpawnEditor],
  template: `
    <header>
      <strong>Ueq Content</strong>
      <nav>
        <button [class.active]="view() === 'items'" (click)="view.set('items')">Items</button>
        <button [class.active]="view() === 'vendors'" (click)="view.set('vendors')">Vendors</button>
        <button [class.active]="view() === 'conversations'" (click)="view.set('conversations')">Conversations</button>
        <button [class.active]="view() === 'mobs'" (click)="view.set('mobs')">Mobs</button>
        <button [class.active]="view() === 'factions'" (click)="view.set('factions')">Factions</button>
        <button [class.active]="view() === 'loot'" (click)="view.set('loot')">Loot</button>
        <button [class.active]="view() === 'xp'" (click)="view.set('xp')">XP</button>
        <button [class.active]="view() === 'spawns'" (click)="view.set('spawns')">Spawns</button>
      </nav>
    </header>

    <div class="body">
      @switch (view()) {
        @case ('items')         { <app-item-editor /> }
        @case ('vendors')       { <app-vendor-editor /> }
        @case ('conversations') { <app-conversation-editor /> }
        @case ('mobs')          { <app-mob-editor /> }
        @case ('factions')      { <app-faction-editor /> }
        @case ('loot')          { <app-loot-editor /> }
        @case ('xp')            { <app-xp-editor /> }
        @case ('spawns')        { <app-spawn-editor /> }
      }
    </div>
  `,
  styles: [`
    :host { display: block; font-family: system-ui, sans-serif; }
    header { display: flex; align-items: center; gap: 1.5rem; padding: 0.6rem 1.25rem;
             border-bottom: 1px solid #e3e3e3; background: #fafafa; }
    nav { display: flex; gap: 0.25rem; }
    nav button { background: none; border: none; padding: 0.35rem 0.8rem; cursor: pointer;
                 border-radius: 4px; color: #555; }
    nav button:hover { background: #eee; }
    nav button.active { background: #1a73e8; color: #fff; }
    .body { max-width: 980px; margin: 1.25rem auto; padding: 0 1rem; }
  `]
})
export class App {
  readonly view = signal<'items' | 'vendors' | 'conversations' | 'mobs' | 'factions' | 'loot' | 'xp' | 'spawns'>('items');
}
