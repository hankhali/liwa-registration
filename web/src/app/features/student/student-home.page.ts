import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { StudentSummary, formatGpa, percentOf, programName } from '../../core/academic/academic.models';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { PlanningService } from '../../core/planning/planning.service';
import { greetingName } from '../../shared/first-name';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { StatusChipComponent } from '../../shared/status-chip.component';

/** Student landing page: studies at a glance, the next step, and the first-login walkthrough (brief §9). */
@Component({
  selector: 'app-student-home',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, TranslocoPipe, PageHeaderComponent, StatusChipComponent],
  template: `
    <app-page-header [heading]="'student.home.welcome' | transloco: { name: name() }" [subtitle]="'student.home.subtitle' | transloco" />

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

    <div class="grid-2">
      <section class="liwa-card studies-card" aria-labelledby="studies-heading" [attr.aria-busy]="summary.isLoading()">
        <h2 id="studies-heading" class="card-title">
          <mat-icon aria-hidden="true">school</mat-icon>
          {{ 'student.home.studies.heading' | transloco }}
        </h2>

        @if (summary.hasValue()) {
          @let s = summary.value();
          <p class="program">{{ program() }}</p>
          <p class="progress-text">
            <strong>{{ 'student.home.studies.progress' | transloco: { earned: s.creditsEarned, required: s.creditsRequired } }}</strong>
          </p>
          <mat-progress-bar mode="determinate" [value]="percent()" [attr.aria-label]="'student.home.studies.progressLabel' | transloco" />
          <p class="liwa-muted small">
            {{ 'student.home.studies.percent' | transloco: { percent: percent() } }}
            @if (s.inProgressCredits > 0) {
              {{ 'student.home.studies.inProgress' | transloco: { credits: s.inProgressCredits } }}
            }
          </p>
          <dl class="facts">
            <div>
              <dt>{{ 'student.home.studies.year' | transloco }}</dt>
              <dd>{{ 'student.home.studies.standing.' + s.standing | transloco }}</dd>
            </div>
            <div>
              <dt>{{ 'student.home.studies.gpa' | transloco }}</dt>
              <dd dir="ltr">{{ s.gpa === null ? ('student.home.studies.noGpa' | transloco) : gpa(s.gpa) }}</dd>
            </div>
            @if (s.advisorName) {
              <div>
                <dt>{{ 'student.home.studies.advisor' | transloco }}</dt>
                <dd>{{ s.advisorName }}</dd>
              </div>
            }
          </dl>
          @if (s.onProbation) {
            <p class="probation"><mat-icon aria-hidden="true">support</mat-icon> {{ 'student.home.studies.probation' | transloco }}</p>
          }
        } @else if (summary.error()) {
          <p class="liwa-muted">{{ (notLoadedYet() ? 'student.home.studies.noRecord' : 'student.home.studies.error') | transloco }}</p>
        } @else {
          <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
        }
      </section>

      <section class="liwa-card next-card" aria-labelledby="next-heading">
        <h2 id="next-heading" class="card-title"><mat-icon aria-hidden="true">event_note</mat-icon> {{ 'student.home.next' | transloco }}</h2>
        @if (plan.hasValue()) {
          @let v = plan.value();
          @if (v.saved; as saved) {
            <p><app-status-chip [status]="saved.status" /></p>
            <p>{{ 'student.home.nextBy.' + saved.status | transloco }}</p>
            <a mat-flat-button routerLink="/student/plan">{{ 'nav.myPlan' | transloco }}</a>
          } @else {
            <p>{{ 'student.home.nextBy.none' | transloco: { n: v.recommendation.totalCredits } }}</p>
            <a mat-flat-button routerLink="/student/planner">{{ 'student.home.seePlan' | transloco }}</a>
          }
          @if (v.hold) {
            <p class="liwa-notice warn">{{ 'student.home.hold' | transloco }}</p>
          }
        } @else if (plan.error()) {
          <p class="liwa-muted">{{ 'plan.notReady' | transloco }}</p>
        } @else {
          <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
        }
      </section>
    </div>
  `,
  styleUrls: ['../../shared/page.scss', './student-home.page.scss'],
})
export class StudentHomePage {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly planning = inject(PlanningService);

  protected readonly walkSteps = [
    { key: 'availability', link: '/student/availability' },
    { key: 'review', link: '/student/planner' },
    { key: 'send', link: '/student/planner' },
  ];

  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
  protected readonly summary = resource({ loader: () => firstValueFrom(this.http.get<StudentSummary>('/api/students/me/summary')) });
  protected readonly plan = resource({
    loader: async () => {
      const p = await this.planning.preferences();
      return this.planning.view(p.availability, p.delivery, p.maxCredits);
    },
  });
  private readonly settings = resource({
    loader: () => firstValueFrom(this.http.get<{ preferredLanguage: string; walkthroughDismissed: boolean }>('/api/me/settings')),
  });
  private readonly dismissed = signal(false);
  protected readonly showWalkthrough = computed(() => this.settings.hasValue() && !this.settings.value().walkthroughDismissed && !this.dismissed());

  protected readonly program = computed(() => (this.summary.hasValue() ? programName(this.summary.value(), this.language.lang()) : ''));
  protected readonly percent = computed(() =>
    this.summary.hasValue() ? percentOf(this.summary.value().creditsEarned, this.summary.value().creditsRequired) : 0,
  );
  /** 404 = the registrar hasn't sent this student's record yet (not an error the student can fix). */
  protected readonly notLoadedYet = computed(() => {
    const e = this.summary.error();
    return e instanceof HttpErrorResponse && e.status === 404;
  });
  protected readonly gpa = formatGpa;

  protected async dismiss(): Promise<void> {
    this.dismissed.set(true);
    await firstValueFrom(this.http.post('/api/me/walkthrough/dismiss', null)).catch(() => undefined);
  }
}
