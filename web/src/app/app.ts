import { Component, OnInit, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from './auth.service';
import { LoadingService } from './loading.service';
import { Login } from './login';
import { ItemEditor } from './item-editor';
import { VendorEditor } from './vendor-editor';
import { ConversationEditor } from './conversation-editor';
import { MobEditor } from './mob-editor';
import { FactionEditor } from './faction-editor';
import { LootEditor } from './loot-editor';
import { XpEditor } from './xp-editor';
import { SpawnEditor } from './spawn-editor';
import { AbilityEditor } from './ability-editor';
import { AbilityTagEditor } from './ability-tag-editor';
import { RaceEditor } from './race-editor';
import { ClassEditor } from './class-editor';
import { WorldClockEditor } from './world-clock-editor';
import { PlacementEditor } from './placement-editor';
import { SpawnGuide } from './spawn-guide';
import { ConversationGuide } from './conversation-guide';
import { QuestGuide } from './quest-guide';
import { CombatGuide } from './combat-guide';
import { ToHitGuide } from './to-hit-guide';
import { AbilitiesGuide } from './abilities-guide';
import { RacesClassesGuide } from './races-classes-guide';
import { BuildDeployGuide } from './build-deploy-guide';
import { PlayPage } from './play-page';
import { CombatSimulator } from './combat-sim/combat-simulator';

type ViewId = 'play' | 'items' | 'vendors' | 'conversations' | 'mobs' | 'factions' | 'loot' | 'xp'
  | 'spawns' | 'placements' | 'abilities' | 'abilityTags' | 'races' | 'classes' | 'worldClock' | 'combatSim'
  | 'docsSpawn' | 'docsConversation' | 'docsQuest' | 'docsCombat' | 'docsToHit' | 'docsAbilities'
  | 'docsRacesClasses' | 'docsBuildDeploy';

interface NavItem { id: ViewId; label: string; icon: string; }

const CONTENT_NAV: NavItem[] = [
  { id: 'items', label: 'Items', icon: 'category' },
  { id: 'vendors', label: 'Vendors', icon: 'storefront' },
  { id: 'conversations', label: 'Conversations', icon: 'forum' },
  { id: 'mobs', label: 'Mobs', icon: 'pets' },
  { id: 'factions', label: 'Factions', icon: 'flag' },
  { id: 'loot', label: 'Loot', icon: 'inventory_2' },
  { id: 'xp', label: 'XP', icon: 'trending_up' },
  { id: 'spawns', label: 'Spawns', icon: 'place' },
  { id: 'placements', label: 'Placements', icon: 'push_pin' },
  { id: 'abilities', label: 'Abilities', icon: 'auto_awesome' },
  { id: 'abilityTags', label: 'Ability Tags', icon: 'sell' },
  { id: 'races', label: 'Races', icon: 'diversity_3' },
  { id: 'classes', label: 'Classes', icon: 'military_tech' },
  { id: 'worldClock', label: 'World Clock', icon: 'brightness_3' },
];

const DOCS_NAV: NavItem[] = [
  { id: 'docsSpawn', label: 'Spawn System', icon: 'place' },
  { id: 'docsConversation', label: 'Conversations', icon: 'forum' },
  { id: 'docsQuest', label: 'Quest Rewards', icon: 'inventory_2' },
  { id: 'docsCombat', label: 'Combat Pipeline', icon: 'swords' },
  { id: 'docsToHit', label: 'To-Hit Formula', icon: 'functions' },
  { id: 'docsAbilities', label: 'Abilities', icon: 'auto_awesome' },
  { id: 'docsRacesClasses', label: 'Races & Classes', icon: 'diversity_3' },
  { id: 'docsBuildDeploy', label: 'Build & Deploy', icon: 'rocket_launch' },
];

/**
 * Shell for the Ueq content editors. A Material sidenav replaces the old flat top-bar of 13 buttons
 * (which overflowed horizontally on desktop) — permanent on wide viewports, a toggleable drawer on
 * narrow ones (CDK `BreakpointObserver`, the same pattern Material's own nav schematic generates).
 * A global progress bar (fed by `loadingInterceptor`) covers every HTTP wait automatically, so
 * individual editors don't each need their own spinner wiring.
 */
@Component({
  selector: 'app-root',
  imports: [
    Login, ItemEditor, VendorEditor, ConversationEditor, MobEditor, FactionEditor, LootEditor,
    XpEditor, SpawnEditor, PlacementEditor, AbilityEditor, AbilityTagEditor, RaceEditor, ClassEditor, WorldClockEditor,
    SpawnGuide, ConversationGuide, QuestGuide, CombatGuide, ToHitGuide, AbilitiesGuide,
    RacesClassesGuide, BuildDeployGuide,
    PlayPage, CombatSimulator,
    MatSidenavModule, MatListModule, MatIconModule, MatButtonModule, MatToolbarModule,
    MatProgressBarModule, MatDividerModule,
  ],
  template: `
    @if (!auth.ready()) {
      <!-- brief flash while the initial /api/auth/me check resolves -->
    } @else if (!auth.username()) {
      <app-login />
    } @else {
      <mat-sidenav-container class="shell">
        <mat-sidenav #drawer class="sidenav" [mode]="isHandset() ? 'over' : 'side'"
                     [opened]="!isHandset()" [fixedInViewport]="isHandset()">
          <div class="brand">Ueq Content</div>

          <mat-nav-list>
            <button mat-list-item [class.active]="view() === 'play'"
                    (click)="go('play', drawer)">
              <mat-icon matListItemIcon>play_arrow</mat-icon>
              <span matListItemTitle>Play</span>
            </button>
          </mat-nav-list>
          <mat-divider />

          <div class="section-label">Content</div>
          <mat-nav-list>
            @for (item of contentNav; track item.id) {
              <button mat-list-item [class.active]="view() === item.id"
                      (click)="go(item.id, drawer)">
                <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
                <span matListItemTitle>{{ item.label }}</span>
              </button>
            }
          </mat-nav-list>
          <mat-divider />

          <div class="section-label">Tools</div>
          <mat-nav-list>
            <button mat-list-item [class.active]="view() === 'combatSim'"
                    (click)="go('combatSim', drawer)">
              <mat-icon matListItemIcon>swords</mat-icon>
              <span matListItemTitle>Combat Simulator</span>
            </button>
          </mat-nav-list>
          <mat-divider />

          <div class="section-label">Documentation</div>
          <mat-nav-list>
            @for (item of docsNav; track item.id) {
              <button mat-list-item [class.active]="view() === item.id"
                      (click)="go(item.id, drawer)">
                <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
                <span matListItemTitle>{{ item.label }}</span>
              </button>
            }
          </mat-nav-list>

          <div class="spacer"></div>
          <mat-divider />
          <div class="who-row">
            <span class="who">{{ auth.username() }}</span>
            <button mat-icon-button (click)="auth.logout()" aria-label="Log out" title="Log out">
              <mat-icon>logout</mat-icon>
            </button>
          </div>
        </mat-sidenav>

        <mat-sidenav-content>
          @if (isHandset()) {
            <mat-toolbar class="mobile-bar">
              <button mat-icon-button (click)="drawer.toggle()" aria-label="Open menu">
                <mat-icon>menu</mat-icon>
              </button>
              <span>Ueq Content</span>
            </mat-toolbar>
          }
          <div class="loading-bar">
            @if (loading.active()) { <mat-progress-bar mode="indeterminate" /> }
          </div>
          <div class="body">
            @switch (view()) {
              @case ('play')          { <app-play-page /> }
              @case ('items')         { <app-item-editor /> }
              @case ('vendors')       { <app-vendor-editor /> }
              @case ('conversations') { <app-conversation-editor /> }
              @case ('mobs')          { <app-mob-editor /> }
              @case ('factions')      { <app-faction-editor /> }
              @case ('loot')          { <app-loot-editor /> }
              @case ('xp')            { <app-xp-editor /> }
              @case ('spawns')        { <app-spawn-editor /> }
              @case ('placements')    { <app-placement-editor /> }
              @case ('abilities')     { <app-ability-editor /> }
              @case ('abilityTags')   { <app-ability-tag-editor /> }
              @case ('races')         { <app-race-editor /> }
              @case ('classes')       { <app-class-editor /> }
              @case ('worldClock')    { <app-world-clock-editor /> }
              @case ('combatSim')     { <app-combat-simulator /> }
              @case ('docsSpawn')        { <app-spawn-guide /> }
              @case ('docsConversation') { <app-conversation-guide /> }
              @case ('docsQuest')        { <app-quest-guide /> }
              @case ('docsCombat')       { <app-combat-guide /> }
              @case ('docsToHit')        { <app-to-hit-guide /> }
              @case ('docsAbilities')    { <app-abilities-guide /> }
              @case ('docsRacesClasses') { <app-races-classes-guide /> }
              @case ('docsBuildDeploy')  { <app-build-deploy-guide /> }
            }
          </div>
        </mat-sidenav-content>
      </mat-sidenav-container>
    }
  `,
  styles: [`
    :host { display: block; height: 100%; font-family: Roboto, system-ui, sans-serif; }
    .shell { height: 100vh; }

    .sidenav { width: 232px; display: flex; flex-direction: column; overflow-y: auto; }
    /* MatListItem doesn't reset native <button> chrome the way MatButton does. */
    .sidenav button[mat-list-item] { border: none; background: transparent; width: 100%;
                                      text-align: left; font: inherit; cursor: pointer; }
    .brand { font-weight: 700; padding: 1.1rem 1rem 0.6rem; font-size: 1.05rem; }
    .section-label { padding: 0.75rem 1rem 0.15rem; font-size: 0.72rem; font-weight: 600;
                      text-transform: uppercase; letter-spacing: 0.04em; color: rgba(0,0,0,0.5); }
    .sidenav button[mat-list-item].active { background: rgba(103, 58, 183, 0.1); }
    .sidenav button[mat-list-item].active mat-icon,
    .sidenav button[mat-list-item].active span { color: #673ab7; }
    .spacer { flex: 1; }
    .who-row { display: flex; align-items: center; justify-content: space-between;
               padding: 0.5rem 0.5rem 0.75rem 1rem; }
    .who { color: rgba(0,0,0,0.6); font-size: 0.85rem; }

    .mobile-bar { position: sticky; top: 0; z-index: 5; }
    .loading-bar { height: 4px; }
    .body { max-width: 980px; margin: 1.25rem auto; padding: 0 1rem 2rem; }
  `],
})
export class App implements OnInit {
  readonly auth = inject(AuthService);
  readonly loading = inject(LoadingService);
  private readonly breakpointObserver = inject(BreakpointObserver);

  readonly view = signal<ViewId>('play');
  readonly contentNav = CONTENT_NAV;
  readonly docsNav = DOCS_NAV;

  readonly isHandset = toSignal(
    this.breakpointObserver.observe(Breakpoints.Handset).pipe(map(r => r.matches)),
    { initialValue: false },
  );

  ngOnInit(): void {
    this.auth.checkSession();
  }

  go(id: ViewId, drawer: { close(): void }): void {
    this.view.set(id);
    if (this.isHandset()) drawer.close();
  }
}
