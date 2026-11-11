import { Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { formatDateTime } from '../core/academic/academic.models';
import { LanguageService } from '../core/i18n/language.service';
import { CourseTitles, courseOnly } from '../core/planning/course-titles';
import { PlanEvent } from '../core/planning/planning.models';
import { isolate } from './bidi';

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
          <span>{{ (student() ? 'historyStudent.' : 'history.') + e.action | transloco: { actor: e.actorName || ('history.system' | transloco), version: e.version ?? '' } }}</span>
          @if (e.fromVersion !== null && !student()) {
            <span class="step">{{ 'history.versionStep' | transloco: { from: e.fromVersion, to: e.version } }}</span>
          }
          @if (e.note && e.action !== 'needsReview') {
            <q>{{ e.note }}</q>
          }
          @if (e.added.length > 0) {
            <span class="small added">+ {{ 'history.added' | transloco }} {{ courses(e.added) }}</span>
          }
          @if (e.removed.length > 0) {
            <span class="small removed">− {{ 'history.removed' | transloco }} {{ courses(e.removed) }}</span>
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
  /** Student wording: no version numbers, no class codes like "(E1)", course titles instead. */
  readonly student = input(false);
  private readonly language = inject(LanguageService);
  private readonly titles = inject(CourseTitles);
  protected readonly lines = computed(() => withVersionSteps(this.events()).reverse());

  /** "BUS220 (E1), FIN201 (01)" → staff: as stored; students: "BUS220 Business Statistics, FIN201 Principles of Finance". */
  protected courses(entries: string[]): string {
    if (!this.student()) return entries.map(isolate).join(', ');
    return entries
      .map((e) => {
        const code = courseOnly(e);
        const title = this.titles.title(code);
        return title ? `${isolate(code)} ${title}` : isolate(code);
      })
      .join(', ');
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }
}
