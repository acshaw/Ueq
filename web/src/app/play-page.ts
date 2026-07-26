import { Component } from '@angular/core';

/**
 * 6.4 — a friendly, non-technical landing spot for getting the game itself (as opposed to the
 * Documentation tab's author-facing Build & Deploy guide, which covers how to *produce* a release).
 * Lives behind the same login as the rest of this app since that's already familiar to whoever
 * authors content here — no separate public page/URL to remember.
 */
@Component({
  selector: 'app-play-page',
  template: `
    <div class="play">
      <h1>Play Ueq</h1>
      <p class="lead">Download the launcher once. From then on, running it always fetches whatever's
        newest and starts the game — no manual redownloading.</p>

      <a class="download" href="/downloads/UeqLauncher.exe" download>Download the Launcher</a>

      <ol class="steps">
        <li>Run <strong>UeqLauncher.exe</strong>.</li>
        <li>The first time, it downloads the full game (a few hundred MB) — later runs only do this
          again if there's something new.</li>
        <li>The game opens automatically once it's ready.</li>
      </ol>

      <p class="note">If Windows shows a "protect your PC" warning, that's SmartScreen being
        cautious about an app from an unrecognized publisher, not a real problem — click
        <em>More info</em> → <em>Run anyway</em>.</p>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .play { max-width: 480px; margin: 2rem auto; text-align: center; }
    h1 { margin: 0 0 0.25rem; }
    .lead { color: #555; margin: 0 0 1.5rem; }
    .download { display: inline-block; background: #1a73e8; color: #fff; text-decoration: none;
                padding: 0.75rem 1.75rem; border-radius: 6px; font-weight: 600; font-size: 1.05rem; }
    .download:hover { background: #1560c2; }
    .steps { text-align: left; margin: 2rem auto 1rem; max-width: 380px; color: #333; }
    .steps li { margin: 0.4rem 0; }
    .note { color: #888; font-size: 0.85rem; margin-top: 1.5rem; }
  `],
})
export class PlayPage {}
