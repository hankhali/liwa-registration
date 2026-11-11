import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { CourseTitles, courseOnly } from '../../core/planning/course-titles';
import { PlanEvent, PlanStatus, classesAndCredits, courseTitle, meetingPatterns, timeRange } from '../../core/planning/planning.models';
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
  // ChangesProposed: Accept / Fix my plan sit under the side-by-side comparison instead.
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
          <!-- What you sent | with your advisor's changes, side by side; then the two ways forward -->
          <section class="liwa-card proposal" aria-labelledby="diff-heading">
            <h2 id="diff-heading">{{ 'myPlan.proposed' | transloco: { advisor: p.actorName } }}</h2>
            <div class="compare">
              @for (side of compare(); track side.key) {
                <div class="side" [attr.aria-labelledby]="'side-' + side.key">
                  <h3 [id]="'side-' + side.key">{{ 'myPlan.compare.' + side.key | transloco }}</h3>
                  <ul>
                    @for (c of side.rows; track c.code) {
                      <li [class.removed]="c.change === 'removed'" [class.added]="c.change === 'added'">
                        @if (c.change) {
                          <mat-icon aria-hidden="true">{{ c.change === 'added' ? 'add_circle' : 'remove_circle' }}</mat-icon>
                          <span class="visually-hidden">{{ 'myPlan.compare.' + c.change | transloco }}</span>
                        }
                        <span class="what">
                          <span><bdi class="code" dir="ltr">{{ c.code }}</bdi> <span class="name">{{ c.title }}</span></span>
                          <span class="liwa-muted small">{{ c.when }}</span>
                        </span>
                      </li>
                    }
                  </ul>
                  <p class="side-total">{{ classesText(side.rows.length, side.credits) }}</p>
                </div>
              }
            </div>
            <div class="actions">
              <button mat-flat-button type="button" class="primary" [disabled]="busy()" (click)="accept()">{{ 'plan.reply.accept' | transloco }}</button>
              <a mat-stroked-button routerLink="/student/planner">{{ 'myPlan.action.Rejected' | transloco }}</a>
            </div>
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

        <!-- (While changes are suggested, the comparison above already lists both versions.) -->
        @if (compare().length === 0) {
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
        }

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
    .compare {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 20rem), 1fr));
      gap: 1rem;
    }
    .side {
      padding: 0.75rem 1rem;
      border: 1px solid var(--liwa-border);
      border-radius: var(--liwa-radius);
    }
    .side h3 {
      margin: 0 0 0.5rem;
    }
    .side ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .side li {
      display: flex;
      gap: 0.5rem;
      align-items: flex-start;
      padding: 0.375rem 0.5rem;
      border-radius: 6px;
    }
    .side li .what {
      display: flex;
      flex-direction: column;
    }
    .side li mat-icon {
      flex: none;
      font-size: 20px;
      width: 20px;
      height: 20px;
      margin-top: 2px;
    }
    li.removed {
      background: var(--liwa-clash);
      color: var(--liwa-danger);
    }
    li.removed .code,
    li.removed .name {
      text-decoration: line-through;
      color: var(--liwa-danger);
    }
    li.added {
      background: var(--liwa-free);
      font-weight: 700;
    }
    li.added mat-icon {
      color: var(--liwa-success);
    }
    .side-total {
      margin: 0.5rem 0 0;
      font-weight: 600;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-top: 1rem;
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
  protected readonly classes = computed(() => this.rowsFor(this.view.value()?.saved?.sectionIds ?? []));

  /**
   * Suggested changes side by side: what the student sent (removed classes struck through) and the plan with the
   * advisor's changes (added classes highlighted). Same course at a different time counts as removed + added.
   */
  protected readonly compare = computed(() => {
    const saved = this.view.value()?.saved;
    if (saved?.status !== 'ChangesProposed' || !saved.previousSectionIds) return [];
    const before = new Set(saved.previousSectionIds);
    const after = new Set(saved.sectionIds);
    const mark = (ids: string[], other: Set<string>, change: 'removed' | 'added') =>
      this.rowsFor(ids).map((r) => ({ ...r, change: other.has(r.id) ? null : change }));
    const side = (key: 'before' | 'after', rows: ReturnType<typeof mark>) => ({ key, rows, credits: rows.reduce((n, r) => n + r.credits, 0) });
    return [side('before', mark(saved.previousSectionIds, after, 'removed')), side('after', mark(saved.sectionIds, before, 'added'))];
  });

  private rowsFor(ids: string[]) {
    const v = this.view.value();
    if (!v) return [];
    const lang = this.language.lang();
    return ids
      .map((id) => v.sections.find((s) => s.id === id))
      .filter((s) => !!s)
      .map((s) => {
        const when =
          s.meetings.length === 0
            ? this.transloco.translate('plan.onlineAnyTime')
            : meetingPatterns(s.meetings)
                .map((p) => `${p.days.map((d) => this.transloco.translate('daysShort.' + d)).join(this.transloco.translate('plan.dayJoin'))} ${timeRange({ day: p.days[0], start: p.start, end: p.end }, lang)}`)
                .join(', ');
        const course = v.courses.find((c) => c.code === s.courseCode);
        return { id: s.id, code: s.courseCode, title: courseTitle(course, lang), credits: course?.credits ?? 0, when };
      })
      .sort((a, b) => a.code.localeCompare(b.code));
  }

  /** "5 classes (15 credits)": students count classes first. */
  protected classesText(classes: number, credits: number): string {
    return classesAndCredits(classes, credits, (k, p) => this.transloco.translate(k, p), this.language.lang());
  }

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
