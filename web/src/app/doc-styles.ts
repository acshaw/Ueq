/**
 * Shared visual language for the in-app author guides (spawn system, quest rewards, and more as
 * they're written). Each guide component applies this to its `.doc` article so they read as one set.
 */
export const DOC_STYLES = `
  :host { display: block; }
  .doc { line-height: 1.6; color: #222; }
  .doc h1 { font-size: 1.9rem; margin: 0 0 0.25rem; }
  .doc .lead { color: #555; margin: 0 0 1.5rem; }
  .doc h2 { font-size: 1.35rem; margin: 2rem 0 0.6rem; padding-bottom: 0.3rem;
            border-bottom: 2px solid #eee; }
  .doc h3 { font-size: 1.1rem; margin: 1.4rem 0 0.4rem; }
  .doc p, .doc li { font-size: 0.95rem; }
  .doc ul, .doc ol { padding-left: 1.4rem; }
  .doc li { margin: 0.2rem 0; }
  .doc code { background: #f2f4f7; border: 1px solid #e3e7ec; border-radius: 4px;
              padding: 0.05rem 0.35rem; font-family: ui-monospace, Menlo, Consolas, monospace;
              font-size: 0.85em; color: #b02a5b; }
  .doc table { border-collapse: collapse; width: 100%; margin: 0.75rem 0; font-size: 0.9rem; }
  .doc th, .doc td { border: 1px solid #e3e7ec; padding: 0.45rem 0.6rem; text-align: left;
                     vertical-align: top; }
  .doc th { background: #f7f9fb; }
  .doc .note { background: #fff8e6; border: 1px solid #f2dfa0; border-radius: 6px;
               padding: 0.6rem 0.85rem; margin: 0.9rem 0; font-size: 0.9rem; }
  .doc .toc { background: #f7f9fb; border: 1px solid #e3e7ec; border-radius: 8px;
              padding: 0.75rem 1rem; margin: 1rem 0 2rem; }
  .doc .toc ol { margin: 0.3rem 0 0; }
  .doc .recipe { border: 1px solid #e3e7ec; border-left: 4px solid #1a73e8; border-radius: 6px;
                 padding: 0.5rem 1rem; margin: 1rem 0; background: #fbfcfe; }
  .doc .recipe h3 { margin-top: 0.5rem; }
  .doc .goal { color: #1a73e8; font-style: italic; margin: 0.2rem 0 0.6rem; }
  .doc footer { margin-top: 2.5rem; padding-top: 1rem; border-top: 1px solid #eee;
                color: #888; font-size: 0.85rem; }
  .doc .tag { display: inline-block; font-size: 0.68rem; font-weight: 700; text-transform: uppercase;
              letter-spacing: 0.03em; padding: 0.05rem 0.4rem; border-radius: 3px; margin-right: 0.45rem;
              vertical-align: 0.06em; white-space: nowrap; }
  .doc .tag.web { background: #e6f0fd; color: #1a56b8; border: 1px solid #cfe0fb; }
  .doc .tag.unity { background: #ececf1; color: #33333a; border: 1px solid #dcdce3; }
  .doc .legend { display: flex; gap: 1rem; flex-wrap: wrap; align-items: center; margin: 0.5rem 0 1rem;
                 font-size: 0.85rem; color: #555; }
`;
