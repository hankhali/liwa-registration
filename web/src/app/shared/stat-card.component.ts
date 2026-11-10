import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/** A big number with a label, optionally linking somewhere (e.g. "3 plans waiting for review"). */
@Component({
  selector: 'app-stat-card',
  imports: [MatIconModule, RouterLink],
  template: `
    @if (link(); as l) {
      <a class="stat liwa-card" [routerLink]="l" [queryParams]="query()" [class.attention]="attention() && value() > 0">
        <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
        <span class="value">{{ value() }}</span>
        <span class="label">{{ label() }}</span>
      </a>
    } @else {
      <div class="stat liwa-card" [class.attention]="attention() && value() > 0">
        <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
        <span class="value">{{ value() }}</span>
        <span class="label">{{ label() }}</span>
      </div>
    }
  `,
  styles: `
    .stat {
      display: grid;
      grid-template-columns: auto 1fr;
      grid-template-rows: auto auto;
      column-gap: 0.75rem;
      align-items: center;
      color: inherit;
      text-decoration: none;
      min-height: 88px;
    }
    a.stat:hover {
      border-color: var(--liwa-accent);
    }
    mat-icon {
      grid-row: span 2;
      color: var(--liwa-accent);
      font-size: 32px;
      width: 32px;
      height: 32px;
    }
    .value {
      font-size: 1.75rem;
      font-weight: 700;
      line-height: 1.1;
    }
    .label {
      color: var(--liwa-text-muted);
    }
    .attention {
      border-inline-start: 4px solid var(--liwa-warning);
    }
  `,
})
export class StatCardComponent {
  readonly label = input.required<string>();
  readonly value = input.required<number>();
  readonly icon = input('insights');
  readonly link = input<string | null>(null);
  readonly query = input<Record<string, string> | null>(null);
  /** Highlight when the number is above zero (something needs doing). */
  readonly attention = input(false);
}
