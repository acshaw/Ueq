import { Component, Input, Output, EventEmitter } from '@angular/core';
import { ADMIN_STYLES } from './admin-styles';

/**
 * Shared CRUD form shell (2.1.1) — a backdrop + centered panel every retrofitted editor opens for
 * create/edit. Each editor projects its own existing (bespoke) form markup via `<ng-content>`
 * unchanged; this component only owns the generic chrome: title, inline error, Save/Delete/Close.
 */
@Component({
  selector: 'app-crud-modal',
  template: `
    @if (open) {
      <div class="backdrop" (click)="close.emit()">
        <div class="panel" (click)="$event.stopPropagation()">
          <div class="head">
            <h2>{{ title }}</h2>
            <button class="closeBtn" (click)="close.emit()" aria-label="Close">✕</button>
          </div>
          @if (error) { <p class="error">{{ error }}</p> }
          <div class="content"><ng-content></ng-content></div>
          <div class="actions">
            <button class="primary" (click)="save.emit()" [disabled]="saveDisabled">Save</button>
            @if (!isNew) { <button class="danger" (click)="delete.emit()">Delete</button> }
          </div>
        </div>
      </div>
    }
  `,
  styles: [ADMIN_STYLES, `
    .backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.35); display: flex;
                align-items: flex-start; justify-content: center; padding: 4vh 1rem; z-index: 100;
                overflow-y: auto; }
    .panel { background: #fff; border-radius: 8px; padding: 1.25rem 1.5rem; width: 100%;
             max-width: 640px; box-shadow: 0 8px 30px rgba(0,0,0,0.2); }
    .head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
    .head h2 { font-size: 1.2rem; margin: 0; }
    .closeBtn { background: none; border: none; font-size: 1rem; color: #888; padding: 0.2rem 0.5rem; }
    .closeBtn:hover { color: #333; }
    .content { margin: 0.5rem 0; }
  `],
})
export class CrudModal {
  @Input() open = false;
  @Input() title = '';
  @Input() isNew = false;
  @Input() error: string | null = null;
  @Input() saveDisabled = false;
  @Output() save = new EventEmitter<void>();
  @Output() delete = new EventEmitter<void>();
  @Output() close = new EventEmitter<void>();
}
