import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** "Nothing here yet" with a friendly sentence and an optional action. */
@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="empty-state" role="status">
      <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
      <div>
        <p class="title">{{ heading() }}</p>
        @if (body()) {
          <p class="liwa-muted">{{ body() }}</p>
        }
        <ng-content />
      </div>
    </div>
  `,
  styles: `
    .empty-state {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
      padding: 1rem 1.25rem;
      border-radius: var(--liwa-radius);
      background: var(--liwa-page);
    }
    mat-icon {
      flex: none;
      color: var(--liwa-accent);
    }
    p {
      margin: 0;
    }
    .title {
      font-weight: 600;
    }
  `,
})
export class EmptyStateComponent {
  readonly heading = input.required<string>();
  readonly body = input<string | null>(null);
  readonly icon = input('inbox');
}
