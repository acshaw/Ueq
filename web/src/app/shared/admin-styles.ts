/**
 * Shared visual language for the retrofitted admin editors (2.1.1). Each editor's own `styles: []`
 * array includes this alongside anything genuinely bespoke to that type. Angular's emulated view
 * encapsulation attributes elements based on the component whose template *declares* them, so this
 * still applies correctly to a form's `<section>`/`<label>` markup even when it's rendered as
 * projected content inside `CrudModal`'s DOM (mirrors the `DOC_STYLES` precedent in `doc-styles.ts`).
 */
export const ADMIN_STYLES = `
  :host { display: block; }
  .toolbar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
  .toolbar h1 { font-size: 1.3rem; margin: 0; }
  section { border: 1px solid #eee; border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 0.75rem; }
  section h3 { margin: 0 0 0.5rem; font-size: 0.95rem; color: #444; }
  label { display: block; margin: 0.35rem 0; font-size: 0.85rem; color: #555; }
  label.check { display: flex; gap: 0.4rem; align-items: center; }
  input, textarea, select { width: 100%; padding: 0.35rem; box-sizing: border-box; font: inherit; }
  label.check input { width: auto; }
  .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0 0.75rem; }
  .rowhead { display: flex; justify-content: space-between; align-items: center; }
  .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
  button { cursor: pointer; padding: 0.4rem 0.8rem; font: inherit; }
  button.small { padding: 0.2rem 0.5rem; }
  .primary { background: #1a73e8; color: #fff; border: none; border-radius: 4px; }
  .danger { background: #fff; color: #c00; border: 1px solid #c00; border-radius: 4px; }
  .muted { color: #999; }
  .error { color: #c00; font-size: 0.85rem; }
  .warn { color: #b8860b; font-size: 0.8rem; margin: 0.25rem 0 0; }
`;
