import { Component } from '@angular/core';
import { DOC_STYLES } from './doc-styles';

const EXTRA_STYLES = `
  .doc pre.formula { background: #f7f9fb; border: 1px solid #e3e7ec; border-radius: 6px;
    padding: 0.8rem 1rem; overflow-x: auto; font-family: ui-monospace, Menlo, Consolas, monospace;
    font-size: 0.85rem; line-height: 1.55; color: #222; }
  .doc pre.formula .c { color: #888; }
  .doc .raw { color: #1a56b8; font-weight: 600; }
  .doc .derived { color: #a0522d; font-weight: 600; }
`;

/** To-Hit & Damage formula reference (5.1.5) — a pure variable/formula glossary, distinct from
 * combat-guide.html's mob-authoring recipes. Static content; markup lives in to-hit-guide.html. */
@Component({
  selector: 'app-to-hit-guide',
  templateUrl: './to-hit-guide.html',
  styles: [DOC_STYLES, EXTRA_STYLES],
})
export class ToHitGuide {}
