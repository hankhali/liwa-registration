import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { PlanEvent, PlanStatus } from '../../core/planning/planning.models';
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

/**
 * My Plan (brief §9): where the plan is (status tracker with times and who acted), the advisor's notes, proposed
 * changes as a diff with Accept, and every version with its full history.
 */
@Component({
  selector: 'app-my-plan-page',
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslocoPipe, EmptyStateComponent, PageHeaderComponent, PlanHistoryComponent, StatusChipComponent],
  template: `
    <app-page-header [heading]="'myPlan.heading' | transloco" [subtitle]="'myPlan.subtitle' | transloco" />

    @if (view.hasValue()) {
      @let v = view.value();
      @if (!v.saved) {
        <app-empty-state icon="event_note" [heading]="'myPlan.none' | transloco" [body]="'myPlan.noneBody' | transloco">
          <a mat-flat-button routerLink="/student/planner">{{ 'myPlan.toPlanner' | transloco }}</a>
        </app-empty-state>
      } @else {
        <section class="liwa-card" aria-labelledby="status-heading">
          <h2 id="status-heading" class="row">
            {{ 'myPlan.status' | transloco }} <app-status-chip [status]="v.saved.status" />
            <span class="liwa-muted small">{{ 'myPlan.version' | transloco: { n: v.saved.version } }}</span>
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

          @if (v.saved.status === 'NeedsReview') {
            <p class="liwa-notice warn">{{ 'myPlan.needsReview' | transloco }} <a routerLink="/student/planner">{{ 'myPlan.toPlanner' | transloco }}</a></p>
          }
          @if (notes().length > 0) {
            <h3>{{ 'myPlan.notes' | transloco }}</h3>
            <ul class="notes">
              @for (n of notes(); track $index) {
                <li><q>{{ n.note }}</q> <span class="liwa-muted small">— {{ n.actorName }}, {{ when(n.at) }}</span></li>
              }
            </ul>
          }
        </section>

        @if (proposal(); as p) {
          <section class="liwa-card proposal" aria-labelledby="diff-heading">
            <h2 id="diff-heading">{{ 'myPlan.proposed' | transloco: { advisor: p.actorName } }}</h2>
            @if (p.note) {
              <q>{{ p.note }}</q>
            }
            <ul class="diff">
              @for (a of p.added; track a) {
                <li class="add"><mat-icon aria-hidden="true">add</mat-icon> <bdi dir="ltr">{{ a }}</bdi> <span class="visually-hidden">{{ 'history.added' | transloco }}</span></li>
              }
              @for (r of p.removed; track r) {
                <li class="remove"><mat-icon aria-hidden="true">remove</mat-icon> <bdi dir="ltr">{{ r }}</bdi> <span class="visually-hidden">{{ 'history.removed' | transloco }}</span></li>
              }
            </ul>
            <div class="actions">
              <button mat-flat-button type="button" [disabled]="busy()" (click)="accept()">{{ 'plan.reply.accept' | transloco }}</button>
              <a mat-button routerLink="/student/planner">{{ 'myPlan.changeInstead' | transloco }}</a>
            </div>
          </section>
        }

        @if (message()) {
          <p class="liwa-notice ok" role="status">{{ 'plan.messages.accepted' | transloco }}</p>
        }

        <section class="liwa-card" aria-labelledby="history-heading">
          <h2 id="history-heading">{{ 'history.heading' | transloco }}</h2>
          <app-plan-history [events]="v.history" />
        </section>
      }
    } @else if (view.error()) {
      <p class="liwa-muted">{{ 'plan.notReady' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    section + section {
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
    .notes {
      padding-inline-start: 1.25rem;
    }
    .proposal q {
      display: block;
      margin-bottom: 0.5rem;
    }
    .diff {
      list-style: none;
      margin: 0;
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
    .actions {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      margin-top: 1rem;
    }
  `,
})
export class MyPlanPage {
  private readonly planning = inject(PlanningService);
  private readonly language = inject(LanguageService);

  protected readonly busy = signal(false);
  protected readonly message = signal(false);
  protected readonly view = resource({
    loader: async () => {
      const prefs = await this.planning.preferences();
      return this.planning.view(prefs.availability, prefs.delivery, prefs.maxCredits);
    },
  });

  protected readonly steps = computed(() => {
    const v = this.view.value();
    return v?.saved ? trackerSteps(v.history, v.saved.status) : [];
  });

  protected readonly notes = computed(() => (this.view.value()?.history ?? []).filter((e) => e.actorRole === 'Advisor' && e.note).reverse());

  /** The advisor's suggested changes, while they wait for the student. */
  protected readonly proposal = computed(() => {
    const v = this.view.value();
    if (v?.saved?.status !== 'ChangesProposed') return null;
    return [...v.history].reverse().find((e) => e.action === 'proposed') ?? null;
  });

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
