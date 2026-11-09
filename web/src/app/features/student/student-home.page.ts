import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, resource } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { StudentSummary, formatGpa, percentOf, programName } from '../../core/academic/academic.models';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { greetingName } from '../../shared/first-name';
import { StudentPlanComponent } from './plan/student-plan.component';

/** Student landing page. Plain language only: no system or technical information for students. */
@Component({
  selector: 'app-student-home',
  imports: [MatIconModule, MatProgressBarModule, TranslocoPipe, StudentPlanComponent],
  template: `
    <header class="page-header">
      <h1>{{ 'student.home.welcome' | transloco: { name: name() } }}</h1>
      <p class="liwa-muted">{{ 'student.home.subtitle' | transloco }}</p>
    </header>

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
        <mat-progress-bar
          mode="determinate"
          [value]="percent()"
          [attr.aria-label]="'student.home.studies.progressLabel' | transloco"
        />
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
          <p class="probation">
            <mat-icon aria-hidden="true">support</mat-icon>
            {{ 'student.home.studies.probation' | transloco }}
          </p>
        }
      } @else if (summary.error()) {
        <p class="liwa-muted">{{ (notLoadedYet() ? 'student.home.studies.noRecord' : 'student.home.studies.error') | transloco }}</p>
      } @else {
        <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
      }
    </section>

    <app-student-plan />
  `,
  styleUrls: ['../../shared/page.scss', './student-home.page.scss'],
})
export class StudentHomePage {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);

  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
  protected readonly summary = resource({ loader: () => firstValueFrom(this.http.get<StudentSummary>('/api/students/me/summary')) });

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
}
