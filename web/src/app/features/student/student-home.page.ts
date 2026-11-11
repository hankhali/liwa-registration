import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { StudentSummary, formatGpa, programName } from '../../core/academic/academic.models';
import { AuthService } from '../../core/auth/auth.service';
import { localDigits } from '../../core/i18n/digits';
import { LanguageService } from '../../core/i18n/language.service';
import { PlanStatus, termLabel } from '../../core/planning/planning.models';
import { PlanningService } from '../../core/planning/planning.service';
import { greetingName } from '../../shared/first-name';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatusChipComponent } from '../../shared/status-chip.component';

/** The one next step per plan status: the label of the gold button and where it goes. */
export const HOME_ACTIONS: Record<PlanStatus | 'none', { label: string; link: string }> = {
  none: { label: 'home.action.none', link: '/student/planner' },
  Draft: { label: 'home.action.Draft', link: '/student/planner' },
  Submitted: { label: 'home.action.Submitted', link: '/student/plan' },
  AdvisorReviewing: { label: 'home.action.Submitted', link: '/student/plan' },
  Approved: { label: 'home.action.Approved', link: '/student/plan' },
  Rejected: { label: 'home.action.Rejected', link: '/student/plan' },
  ChangesProposed: { label: 'home.action.ChangesProposed', link: '/student/plan' },
  NeedsReview: { label: 'home.action.NeedsReview', link: '/student/plan' },
};

/**
 * Student Home (UX pass): a single answer — "Spring 2027: your plan is Sent back with comments." — with what that
 * means and one gold button for the next step, then three big numbers (credits done, GPA, graduation date).
 * Anything else (hold, probation, the first-visit steps, program and advisor) sits below.
 */
@Component({
  selector: 'app-student-home',
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslocoPipe, StatCardComponent, StatusChipComponent],
  template: `
    <p class="hello liwa-muted">{{ 'home.hello' | transloco: { name: name() } }}</p>

    @if (plan.hasValue()) {
      @let v = plan.value();
      @let status = v.saved?.status ?? 'none';
      <section class="answer liwa-card" aria-labelledby="answer-heading">
        <div class="answer-text">
          <h1 id="answer-heading">
            @if (v.saved) {
              {{ 'home.answer' | transloco: { term: term() } }}
              <app-status-chip [status]="v.saved.status" [student]="true" />
            } @else {
              {{ 'home.answerNone' | transloco: { term: term() } }}
            }
          </h1>
          <p class="meaning">
            @if (v.saved) {
              {{ 'studentStatus.meaning.' + v.saved.status | transloco }}
            } @else {
              {{ 'home.meaningNone' | transloco: { n: v.recommendation.totalCredits } }}
            }
          </p>
        </div>
        <a mat-flat-button class="primary" [routerLink]="actions[status].link">{{ actions[status].label | transloco }}</a>
      </section>
    } @else if (plan.error()) {
      <section class="answer liwa-card">
        <h1>{{ 'home.notReady' | transloco }}</h1>
        <p class="meaning">{{ 'plan.notReady' | transloco }}</p>
      </section>
    } @else {
      <section class="answer liwa-card" aria-busy="true"><p class="liwa-muted">{{ 'common.loading' | transloco }}</p></section>
    }

    <div class="numbers">
      <app-stat-card icon="task_alt" [value]="creditsValue()" [label]="'home.numbers.credits' | transloco" />
      <app-stat-card icon="grade" [value]="gpaValue()" [label]="'home.numbers.gpa' | transloco" />
      <app-stat-card icon="school" [value]="graduation()" [label]="'home.numbers.graduation' | transloco" link="/student/graduation" />
    </div>

    @if (plan.value()?.hold) {
      <p class="liwa-notice warn">{{ 'student.home.hold' | transloco }}</p>
    }
    @if (summary.value()?.onProbation) {
      <p class="liwa-notice"><mat-icon aria-hidden="true" class="inline-icon">support</mat-icon> {{ 'student.home.studies.probation' | transloco }}</p>
    }

    @if (showWalkthrough()) {
      <section class="liwa-card walkthrough" aria-labelledby="walk-heading">
        <div class="walk-head">
          <h2 id="walk-heading">{{ 'walkthrough.heading' | transloco }}</h2>
          <button mat-button type="button" (click)="dismiss()">{{ 'walkthrough.dismiss' | transloco }}</button>
        </div>
        <ol class="steps">
          @for (s of walkSteps; track s.key; let i = $index) {
            <li>
              <span class="step-number" aria-hidden="true">{{ i + 1 }}</span>
              <span class="step-text">
                <a [routerLink]="s.link"><strong>{{ 'student.home.steps.' + s.key + '.title' | transloco }}</strong></a>
                <span class="liwa-muted">{{ 'student.home.steps.' + s.key + '.body' | transloco }}</span>
              </span>
            </li>
          }
        </ol>
      </section>
    }

    @if (summary.hasValue()) {
      @let s = summary.value();
      <dl class="facts liwa-muted">
        <div><dt>{{ 'home.facts.program' | transloco }}</dt><dd>{{ program() }}</dd></div>
        <div><dt>{{ 'student.home.studies.year' | transloco }}</dt><dd>{{ 'student.home.studies.standing.' + s.standing | transloco }}</dd></div>
        @if (s.advisorName) {
          <div><dt>{{ 'student.home.studies.advisor' | transloco }}</dt><dd>{{ s.advisorName }}</dd></div>
        }
      </dl>
    } @else if (summary.error()) {
      <p class="liwa-muted">{{ (notLoadedYet() ? 'student.home.studies.noRecord' : 'student.home.studies.error') | transloco }}</p>
    }
  `,
  styleUrls: ['../../shared/page.scss', './student-home.page.scss'],
})
export class StudentHomePage {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly planning = inject(PlanningService);
  private readonly transloco = inject(TranslocoService);

  protected readonly actions = HOME_ACTIONS;
  protected readonly walkSteps = [
    { key: 'availability', link: '/student/availability' },
    { key: 'review', link: '/student/planner' },
    { key: 'send', link: '/student/planner' },
  ];

  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
  protected readonly summary = resource({ loader: () => firstValueFrom(this.http.get<StudentSummary>('/api/students/me/summary')) });
  protected readonly plan = resource({ loader: async () => (await this.planning.currentView()).view });
  private readonly settings = resource({
    loader: () => firstValueFrom(this.http.get<{ preferredLanguage: string; walkthroughDismissed: boolean }>('/api/me/settings')),
  });
  private readonly dismissed = signal(false);
  /** The three first-visit steps only matter before the first plan is saved. */
  protected readonly showWalkthrough = computed(
    () => this.settings.hasValue() && !this.settings.value().walkthroughDismissed && !this.dismissed() && this.plan.hasValue() && !this.plan.value().saved,
  );

  protected readonly term = computed(() => {
    const v = this.plan.value();
    return v ? termLabel(v.termCode, (k) => this.transloco.translate(k), this.language.lang()) : '';
  });
  protected readonly program = computed(() => (this.summary.hasValue() ? programName(this.summary.value(), this.language.lang()) : ''));
  protected readonly creditsValue = computed(() => {
    const s = this.summary.value();
    return s ? this.transloco.translate('home.numbers.creditsValue', { earned: localDigits(String(s.creditsEarned), this.language.lang()), required: localDigits(String(s.creditsRequired), this.language.lang()) }) : '–';
  });
  protected readonly gpaValue = computed(() => {
    const s = this.summary.value();
    if (!s) return '–';
    return s.gpa === null ? this.transloco.translate('student.home.studies.noGpa') : formatGpa(s.gpa);
  });
  /** Graduation date of the student's own plan when there is one, otherwise of the recommended plan (no summer). */
  protected readonly graduation = computed(() => {
    const v = this.plan.value();
    if (!v) return '–';
    const p = (v.saved && v.savedProjection) || v.recommendation.projection;
    const code = p.withoutSummer.graduationTerm;
    return code ? termLabel(code, (k) => this.transloco.translate(k), this.language.lang()) : this.transloco.translate('advisor.review.unknownTerm');
  });
  /** 404 = the registrar hasn't sent this student's record yet (not an error the student can fix). */
  protected readonly notLoadedYet = computed(() => {
    const e = this.summary.error();
    return e instanceof HttpErrorResponse && e.status === 404;
  });

  protected async dismiss(): Promise<void> {
    this.dismissed.set(true);
    await firstValueFrom(this.http.post('/api/me/walkthrough/dismiss', null)).catch(() => undefined);
  }
}
