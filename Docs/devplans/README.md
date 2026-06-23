# Devplans

One devplan per roadmap item, written and **reviewed before implementation**. See the
workflow and numbering scheme in [`/roadmap.md`](../../roadmap.md).

## Naming

`<id>-<kebab-slug>.md`

- `<id>` is the roadmap item id from `roadmap.md`: `1.1`, `1.2`, … or an inserted
  third-level id `1.2.1`, `1.2.2`, … for work wedged between two items.
- Examples: `1.1-postgres-docker-env.md`, `1.2.1-save-queue-backpressure.md`.

Files sort in implementation order because dotted-decimal ids sort naturally
(`1.2 < 1.2.1 < 1.3`).

## What a devplan contains

- **Goal** — what this item delivers and why.
- **Approach** — the design/decisions.
- **Schema / API changes** — DB tables, serialized data, public methods.
- **Files touched** — new + modified.
- **Test plan** — how we verify it (editor steps + what to watch).
- **Risks / open questions.**

## Workflow

1. Pick the next item from `roadmap.md`.
2. Write its devplan here.
3. Review + approve.
4. Implement.
5. Mark the item ✅ in `roadmap.md` and add it to the devplan log there.

---

`ability-system.md` predates this convention (unnumbered, historical) — leave as-is.
