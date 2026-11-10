import { Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { formatDateTime } from '../core/academic/academic.models';
import { LanguageService } from '../core/i18n/language.service';
import { PlanEvent } from '../core/planning/planning.models';

/** One history line with the version step it made ("Version 1 → 2"), if any. */
export interface HistoryLine extends PlanEvent {
  fromVersion: number | null;
}

/** Adds the version each step started from, so a change reads "Version 1 → 2" (brief §10). */
export function withVersionSteps(events: PlanEvent[]): HistoryLine[] {
  let last: number | null = null;
  return events.map((e) => {
    const line = { ...e, fromVersion: e.version !== null && last !== null && e.version !== last ? last : null };
    if (e.version !== null) last = e.version;
    return line;
  });
}

/** The plan's full history, newest first, visible to both the student and the advisor. */
@Component({
  selector: 'app-plan-history',
  imports: [MatIconModule, TranslocoPipe],
  template: `
    <ol class="history">
      @for (e of lines(); track $index) {
        <li>
          <span class="liwa-muted small">{{ when(e.at) }}</span>
          <span>{{ 'history.' + e.action | transloco: { actor: e.actorName || ('history.system' | transloco), version: e.version ?? '' } }}</span>
          @if (e.fromVersion !== null) {
            <span class="step">{{ 'history.versionStep' | transloco: { from: e.fromVersion, to: e.version } }}</span>
          }
          @if (e.note && e.action !== 'needsReview') {
            <q>{{ e.note }}</q>
          }
          @if (e.added.length > 0) {
            <span class="small added">+ {{ 'history.added' | transloco }} <bdi dir="ltr">{{ e.added.join(', ') }}</bdi></span>
          }
          @if (e.removed.length > 0) {
            <span class="small removed">− {{ 'history.removed' | transloco }} <bdi dir="ltr">{{ e.removed.join(', ') }}</bdi></span>
          }
        </li>
      } @empty {
        <li class="liwa-muted">{{ 'history.empty' | transloco }}</li>
      }
    </ol>
  `,
  styles: `
    .history {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
      padding: 0.5rem 0 0.5rem 0.875rem;
      border-inline-start: 2px solid var(--liwa-border);
    }
    .small {
      font-size: 0.8125rem;
    }
    .step {
      font-weight: 600;
      font-size: 0.8125rem;
    }
    .added {
      color: var(--liwa-success);
    }
    .removed {
      color: var(--liwa-danger);
    }
  `,
})
export class PlanHistoryComponent {
  readonly events = input<PlanEvent[]>([]);
  private readonly language = inject(LanguageService);
  protected readonly lines = computed(() => withVersionSteps(this.events()).reverse());

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }
}
