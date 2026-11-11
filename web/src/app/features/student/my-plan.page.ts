import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { CourseTitles, courseOnly } from '../../core/planning/course-titles';
import { PlanEvent, PlanStatus, courseTitle, meetingPatterns, timeRange } from '../../core/planning/planning.models';
import { PlanningService } from '../../core/planning/planning.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { PlanHistoryComponent } from '../../shared/plan-history.component';
import { StatusChipComponent } from '../../shared/status-chip.component';

type Step = 'saved' | 'submitted' | 'reviewing' | 'decided';

/** The steps of the status tracker for the current round (since the last time the plan was sent). */
export function trackerSteps(history: PlanEvent[], status: PlanStatus): { step: Step; done: boolean; event: PlanEvent | null }[] {
  const lastSent = history.map((e) => e.action).lastIndexOf('submitted');
  const round = lastSent < 0 ? history : history.slice(lastSent);
  const find = (actions: string[]) => [...round].reverse().find((e) => actions.includes(e.action)) ?? null;
  const decided = find(['approved', 'rejected', 'proposed', 'accepted', 'needsReview']);
  return [
    { step: 'saved', done: history.length > 0, event: lastSent < 0 ? find(['saved']) : null },
    { step: 'submitted', done: lastSent >= 0, event: lastSent >= 0 ? history[lastSent] : null },
    { step: 'reviewing', done: !!find(['reviewing']) || !!decided, event: find(['reviewing']) },
    { step: 'decided', done: !!decided && status !== 'Submitted' && status !== 'AdvisorReviewing', event: decided },
  ];
}

/** The one next step on My Plan per status (gold, top right). Accept is a button; the others go to the planner. */
export const MY_PLAN_ACTIONS: Partial<Record<PlanStatus, { label: string; link: string | null }>> = {
  Draft: { label: 'myPlan.action.Draft', link: '/student/planner' },
  Rejected: { label: 'myPlan.action.Rejected', link: '/student/planner' },
  ChangesProposed: { label: 'plan.reply.accept', link: null },
  NeedsReview: { label: 'myPlan.action.NeedsReview', link: '/student/planner' },
};

/**
 * My Plan (brief §9, UX pass): when the advisor has replied, their note comes first with the one button that does the
 * next step (fix the plan, accept the changes, check it again). Then the status in student words with what it
 * means, the tracker (times and who acted), the classes in the plan, suggested changes as a diff, and the history.
 */
@Component({
  selector: 'app-my-plan-page',
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslocoPipe, EmptyStateComponent, PageHeaderComponent, PlanHistoryComponent, StatusChipComponent],
  template: `
    @let v = view.value();
    <app-page-header [heading]="'myPlan.heading' | transloco" [subtitle]="(v?.saved ? 'studentStatus.meaning.' + v!.saved!.status : 'myPlan.subtitle') | transloco">
      @if (action(); as a) {
        @if (a.link) {
          <a mat-flat-button class="primary" [routerLink]="a.link">{{ a.label | transloco }}</a>
        } @else {
          <button mat-flat-button type="button" class="primary" [disabled]="busy()" (click)="accept()">{{ a.label | transloco }}</button>
        }
      }
    </app-page-header>

    @if (view.hasValue()) {
      @if (!v!.saved) {
        <app-empty-state icon="event_note" [heading]="'myPlan.none' | transloco" [body]="'myPlan.noneBody' | transloco">
          <a mat-flat-button routerLink="/student/planner">{{ 'myPlan.toPlanner' | transloco }}</a>
        </app-empty-state>
      } @else {
        @let saved = v!.saved!;

        <!-- The advisor's reply first: what they said, from whom, when -->
        @if (reply(); as r) {
          <section class="reply" [class.reply-back]="saved.status !== 'ChangesProposed'" aria-labelledby="reply-heading">
            <h2 id="reply-heading">{{ 'myPlan.replyHeading.' + saved.status | transloco: { advisor: r.actorName || ('plan.yourAdvisor' | transloco) } }}</h2>
            @if (r.note) {
              <q class="note">{{ r.note }}</q>
            }
            <span class="liwa-muted small">{{ r.actorName }} · {{ when(r.at) }}</span>
            <p class="next">{{ 'myPlan.replyNext.' + saved.status | transloco }}</p>
          </section>
        }

        @if (proposal(); as p) {
          <section class="liwa-card proposal" aria-labelledby="diff-heading">
            <h2 id="diff-heading">{{ 'myPlan.proposed' | transloco: { advisor: p.actorName } }}</h2>
            <ul class="diff">
              @for (a of p.added; track a) {
                <li class="add"><mat-icon aria-hidden="true">add</mat-icon> <span class="visually-hidden">{{ 'history.added' | transloco }}</span> {{ course(a) }}</li>
              }
              @for (r of p.removed; track r) {
                <li class="remove"><mat-icon aria-hidden="true">remove</mat-icon> <span class="visually-hidden">{{ 'history.removed' | transloco }}</span> {{ course(r) }}</li>
              }
            </ul>
            <a mat-button routerLink="/student/planner">{{ 'myPlan.changeInstead' | transloco }}</a>
          </section>
        }

        @if (message()) {
          <p class="liwa-notice ok" role="status">{{ 'plan.messages.accepted' | transloco }}</p>
        }

        <section class="liwa-card" aria-labelledby="status-heading">
          <h2 id="status-heading" class="row">
            {{ 'myPlan.status' | transloco }} <app-status-chip [status]="saved.status" [student]="true" />
          </h2>
          <ol class="tracker">
            @for (s of steps(); track s.step) {
              <li [class.done]="s.done">
                <mat-icon aria-hidden="true">{{ s.done ? 'check_circle' : 'radio_button_unchecked' }}</mat-icon>
                <div>
                  <strong>{{ 'myPlan.steps.' + s.step | transloco }}</strong>
                  @if (s.event; as e) {
                    <span class="liwa-muted small">{{ when(e.at) }} · {{ e.actorName || ('history.system' | transloco) }}</span>
                  }
                </div>
              </li>
            }
          </ol>
        </section>

        <section class="liwa-card" aria-labelledby="classes-heading">
          <h2 id="classes-heading">{{ 'myPlan.classes' | transloco: { n: classes().length } }}</h2>
          <ul class="classes">
            @for (c of classes(); track c.code) {
              <li>
                <span><bdi class="code" dir="ltr">{{ c.code }}</bdi> <strong>{{ c.title }}</strong></span>
                <span class="liwa-muted">{{ c.when }}</span>
              </li>
            }
          </ul>
        </section>

        <section class="liwa-card" aria-labelledby="history-heading">
          <h2 id="history-heading">{{ 'history.heading' | transloco }}</h2>
          <app-plan-history [events]="v!.history" [student]="true" />
        </section>
      }
    } @else if (view.error()) {
      <p class="liwa-muted">{{ 'plan.notReady' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    section + section,
    .reply + section {
      margin-top: 1rem;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .small {
      font-size: 0.8125rem;
    }
    .primary {
      min-height: 48px;
      font-size: 1rem;
    }
    .reply {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      padding: 1rem 1.25rem;
      border-radius: var(--liwa-radius);
      border-inline-start: 6px solid var(--liwa-chip-proposed-fg);
      background: var(--liwa-chip-proposed-bg);
      color: var(--liwa-text);
    }
    .reply-back {
      border-inline-start-color: var(--liwa-danger);
      background: var(--liwa-clash);
    }
    .reply h2 {
      margin: 0;
      font-size: 1.125rem;
    }
    .note {
      font-size: 1.25rem;
      font-style: italic;
    }
    .next {
      margin: 0.25rem 0 0;
      font-weight: 600;
    }
    .tracker {
      list-style: none;
      margin: 1rem 0 0;
      padding: 0;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
      gap: 0.75rem;
    }
    .tracker li {
      display: flex;
      gap: 0.5rem;
      color: var(--liwa-text-muted);
    }
    .tracker li.done {
      color: var(--liwa-text);
    }
    .tracker li.done mat-icon {
      color: var(--liwa-success);
    }
    .tracker div {
      display: flex;
      flex-direction: column;
    }
    .classes {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .classes li {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 0.25rem 1rem;
      padding: 0.5rem 0;
      border-bottom: 1px solid var(--liwa-border);
    }
    .classes li:last-child {
      border-bottom: 0;
    }
    .code {
      font-weight: 700;
      color: var(--liwa-primary);
      margin-inline-end: 0.375rem;
    }
    .diff {
      list-style: none;
      margin: 0 0 0.5rem;
      padding: 0;
    }
    .diff li {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.25rem 0;
      font-weight: 600;
    }
    .add {
      color: var(--liwa-success);
    }
    .remove {
      color: var(--liwa-danger);
    }
    @media (max-width: 600px) {
      .primary {
        width: 100%;
      }
    }
  `,
})
export class MyPlanPage {
  private readonly planning = inject(PlanningService);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly titles = inject(CourseTitles);

  protected readonly busy = signal(false);
  protected readonly message = signal(false);
  protected readonly view = resource({ loader: async () => (await this.planning.currentView()).view });

  protected readonly steps = computed(() => {
    const v = this.view.value();
    return v?.saved ? trackerSteps(v.history, v.saved.status) : [];
  });

  protected readonly action = computed(() => {
    const status = this.view.value()?.saved?.status;
    return status ? (MY_PLAN_ACTIONS[status] ?? null) : null;
  });

  /** The advisor's latest reply while it waits for the student (sent back, changes suggested, grades posted). */
  protected readonly reply = computed(() => {
    const v = this.view.value();
    const status = v?.saved?.status;
    if (!v || (status !== 'Rejected' && status !== 'ChangesProposed' && status !== 'NeedsReview')) return null;
    const actions = status === 'NeedsReview' ? ['needsReview'] : status === 'Rejected' ? ['rejected'] : ['proposed'];
    return [...v.history].reverse().find((e) => actions.includes(e.action)) ?? null;
  });

  /** The advisor's suggested changes, while they wait for the student. */
  protected readonly proposal = computed(() => {
    const v = this.view.value();
    if (v?.saved?.status !== 'ChangesProposed') return null;
    return [...v.history].reverse().find((e) => e.action === 'proposed') ?? null;
  });

  /** The classes in the saved plan: code, title, days and times. */
  protected readonly classes = computed(() => {
    const v = this.view.value();
    if (!v?.saved) return [];
    const lang = this.language.lang();
    return v.saved.sectionIds
      .map((id) => v.sections.find((s) => s.id === id))
      .filter((s) => !!s)
      .map((s) => {
        const when =
          s.meetings.length === 0
            ? this.transloco.translate('plan.onlineAnyTime')
            : meetingPatterns(s.meetings)
                .map((p) => `${p.days.map((d) => this.transloco.translate('daysShort.' + d)).join(this.transloco.translate('plan.dayJoin'))} ${timeRange({ day: p.days[0], start: p.start, end: p.end }, lang)}`)
                .join(', ');
        return { code: s.courseCode, title: courseTitle(v.courses.find((c) => c.code === s.courseCode), lang), when };
      })
      .sort((a, b) => a.code.localeCompare(b.code));
  });

  protected course(entry: string): string {
    const code = courseOnly(entry);
    const title = this.titles.title(code);
    return title ? `${code} ${title}` : code;
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  protected async accept(): Promise<void> {
    this.busy.set(true);
    try {
      await this.planning.accept();
      this.message.set(true);
      this.view.reload();
    } finally {
      this.busy.set(false);
    }
  }
}
