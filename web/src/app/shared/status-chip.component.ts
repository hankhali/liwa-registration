import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PlanStatus } from '../core/planning/planning.models';

/** Icons per status: colour is never the only signal (WCAG 1.4.1). */
export const STATUS_ICONS: Record<PlanStatus, string> = {
  Draft: 'edit_note',
  Submitted: 'schedule_send',
  AdvisorReviewing: 'visibility',
  Approved: 'check_circle',
  Rejected: 'undo',
  ChangesProposed: 'rate_review',
  NeedsReview: 'error',
};

/** A plan status with a fixed colour per status (same on every screen, both roles). */
@Component({
  selector: 'app-status-chip',
  imports: [TranslocoPipe],
  template: `<span class="chip" [class]="'chip status-' + status()"><span class="material-icons" aria-hidden="true">{{ icons[status()] }}</span>{{ 'planStatus.' + status() | transloco }}</span>`,
  styles: `
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0.125rem 0.625rem 0.125rem 0.5rem;
      border-radius: 999px;
      font-size: 0.8125rem;
      font-weight: 600;
      white-space: nowrap;
      border: 1px solid transparent;
    }
    .material-icons {
      font-size: 16px;
    }
    .status-Draft { background: var(--liwa-chip-draft-bg); color: var(--liwa-chip-draft-fg); }
    .status-Submitted { background: var(--liwa-chip-submitted-bg); color: var(--liwa-chip-submitted-fg); }
    .status-AdvisorReviewing { background: var(--liwa-chip-reviewing-bg); color: var(--liwa-chip-reviewing-fg); }
    .status-Approved { background: var(--liwa-chip-approved-bg); color: var(--liwa-chip-approved-fg); }
    .status-Rejected { background: var(--liwa-chip-rejected-bg); color: var(--liwa-chip-rejected-fg); }
    .status-ChangesProposed { background: var(--liwa-chip-proposed-bg); color: var(--liwa-chip-proposed-fg); }
    .status-NeedsReview { background: var(--liwa-chip-needs-bg); color: var(--liwa-chip-needs-fg); border-color: currentColor; }
  `,
})
export class StatusChipComponent {
  readonly status = input.required<PlanStatus>();
  protected readonly icons = STATUS_ICONS;
}
