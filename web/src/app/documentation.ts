import { Component, signal } from '@angular/core';
import { SpawnGuide } from './spawn-guide';
import { QuestGuide } from './quest-guide';
import { ConversationGuide } from './conversation-guide';
import { CombatGuide } from './combat-guide';
import { AbilitiesGuide } from './abilities-guide';
import { RacesClassesGuide } from './races-classes-guide';

/**
 * Documentation tab — in-app reference guides for content authors. A small sub-nav switches between
 * guides (spawn system, conversations, quest rewards, combat pipeline, abilities, races & classes, …);
 * each guide is its own component with the shared DOC_STYLES.
 */
@Component({
  selector: 'app-documentation',
  imports: [SpawnGuide, ConversationGuide, QuestGuide, CombatGuide, AbilitiesGuide, RacesClassesGuide],
  template: `
    <nav class="guides">
      <button [class.active]="guide() === 'spawn'" (click)="guide.set('spawn')">Spawn System</button>
      <button [class.active]="guide() === 'conversation'" (click)="guide.set('conversation')">Conversations</button>
      <button [class.active]="guide() === 'quest'" (click)="guide.set('quest')">Quest Rewards</button>
      <button [class.active]="guide() === 'combat'" (click)="guide.set('combat')">Combat Pipeline</button>
      <button [class.active]="guide() === 'abilities'" (click)="guide.set('abilities')">Abilities</button>
      <button [class.active]="guide() === 'racesClasses'" (click)="guide.set('racesClasses')">Races &amp; Classes</button>
    </nav>
    @switch (guide()) {
      @case ('spawn') { <app-spawn-guide /> }
      @case ('conversation') { <app-conversation-guide /> }
      @case ('quest') { <app-quest-guide /> }
      @case ('combat') { <app-combat-guide /> }
      @case ('abilities') { <app-abilities-guide /> }
      @case ('racesClasses') { <app-races-classes-guide /> }
    }
  `,
  styles: [`
    :host { display: block; }
    .guides { display: flex; gap: 0.25rem; margin-bottom: 1.5rem; border-bottom: 1px solid #eee;
              padding-bottom: 0.75rem; }
    .guides button { background: none; border: 1px solid #dfe3e8; padding: 0.35rem 0.9rem; cursor: pointer;
                     border-radius: 999px; color: #555; font-size: 0.9rem; }
    .guides button:hover { background: #f2f4f7; }
    .guides button.active { background: #1a73e8; border-color: #1a73e8; color: #fff; }
  `]
})
export class Documentation {
  readonly guide = signal<'spawn' | 'conversation' | 'quest' | 'combat' | 'abilities' | 'racesClasses'>('spawn');
}
