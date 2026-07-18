import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AbilityTag, AbilityTagService, emptyAbilityTag, ABILITY_TAG_GRID_COLUMNS, ABILITY_TAG_SEARCH_FIELDS } from './ability-tag.service';
import { ContentGrid } from './shared/content-grid';
import { CrudModal } from './shared/crud-modal';
import { ADMIN_STYLES } from './shared/admin-styles';

/**
 * The web Ability Tag Editor (M2.9). A tag is a pure semantic label (id + display name) referenced by
 * an ability's own tags list and by its cooldown links (the shared-timer key) — flat, no children.
 */
@Component({
  selector: 'app-ability-tag-editor',
  imports: [FormsModule, ContentGrid, CrudModal],
  template: `
    <div class="toolbar">
      <h1>Ability tags</h1>
      <button class="primary" (click)="newTag()">+ New</button>
    </div>
    @if (error() && !modalOpen) { <p class="error">{{ error() }}</p> }

    <app-content-grid
      [rows]="tags()"
      [columns]="columns"
      [searchFields]="searchFields"
      (rowClick)="select($event)"
    />

    <app-crud-modal
      [open]="modalOpen"
      [title]="isNew ? 'New ability tag' : (model?.tagId ?? '')"
      [isNew]="isNew"
      [error]="modalOpen ? error() : null"
      [saveDisabled]="isNew && !model?.tagId?.trim()"
      (save)="save()"
      (delete)="remove()"
      (close)="closeModal()"
    >
      @if (model) {
        <section>
          <h3>Tag</h3>
          <label>tag_id <input [(ngModel)]="model.tagId" name="tagId" [disabled]="!isNew" placeholder="martialability" /></label>
          <label>Display name <input [(ngModel)]="model.displayName" name="displayName" /></label>
        </section>
      }
    </app-crud-modal>
  `,
  styles: [ADMIN_STYLES],
})
export class AbilityTagEditor implements OnInit {
  private readonly api = inject(AbilityTagService);

  readonly tags = signal<AbilityTag[]>([]);
  readonly error = signal<string | null>(null);
  model: AbilityTag | null = null;
  isNew = false;
  modalOpen = false;

  readonly columns = ABILITY_TAG_GRID_COLUMNS;
  readonly searchFields = ABILITY_TAG_SEARCH_FIELDS;

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.api.getAll().subscribe({
      next: rows => { this.tags.set(rows); this.error.set(null); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  newTag(): void { this.model = emptyAbilityTag(); this.isNew = true; this.modalOpen = true; }

  select(t: AbilityTag): void {
    this.model = { ...t };
    this.isNew = false;
    this.modalOpen = true;
  }

  closeModal(): void { this.modalOpen = false; this.model = null; this.error.set(null); }

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
    this.api.delete(this.model.tagId).subscribe({
      next: () => { this.model = null; this.modalOpen = false; this.reload(); },
      error: err => this.error.set(this.describe(err)),
    });
  }

  private describe(err: unknown): string {
    const e = err as { status?: number; error?: string; message?: string };
    if (e?.status === 0) return 'Cannot reach the API — is the .NET api project running on http://localhost:5144?';
    if (e?.status === 409) return 'An ability tag with that id already exists.';
    if (e?.status === 500) return 'Request failed — this tag may still be referenced by an ability (cooldown link or tag).';
    return (typeof e?.error === 'string' ? e.error : e?.message) ?? 'Request failed.';
  }
}
