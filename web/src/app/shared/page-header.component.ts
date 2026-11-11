import { Component, input } from '@angular/core';

/**
 * Page title + one-line subtitle (on student screens: what to do here), with an actions slot on the end side —
 * where the screen's one gold primary action goes (top right; full width on phones).
 */
@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div class="text">
        <h1>{{ heading() }}</h1>
        @if (subtitle()) {
          <p class="liwa-muted">{{ subtitle() }}</p>
        }
      </div>
      <div class="actions"><ng-content /></div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem 1rem;
      margin-bottom: 1.5rem;
    }
    h1 {
      margin: 0 0 0.25rem;
    }
    p {
      margin: 0;
    }
    .actions:empty {
      display: none;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    @media (max-width: 600px) {
      .actions {
        width: 100%;
      }
    }
  `,
})
export class PageHeaderComponent {
  readonly heading = input.required<string>();
  readonly subtitle = input<string | null>(null);
}
