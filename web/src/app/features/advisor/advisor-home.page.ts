import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { AdviseeSummary, formatGpa, programName } from '../../core/academic/academic.models';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { greetingName } from '../../shared/first-name';

@Component({
  selector: 'app-advisor-home',
  imports: [MatIconModule, TranslocoPipe],
  template: `
    <header class="page-header">
      <h1>{{ 'advisor.home.welcome' | transloco: { name: name() } }}</h1>
      <p class="liwa-muted">{{ 'advisor.home.subtitle' | transloco }}</p>
    </header>

    <div class="grid">
      <section class="liwa-card" aria-labelledby="attention-heading">
        <h2 id="attention-heading" class="card-title">
          <mat-icon aria-hidden="true">priority_high</mat-icon>
          {{ 'advisor.home.needsAttention' | transloco }}
        </h2>
        <p class="empty">
          <mat-icon aria-hidden="true">check_circle</mat-icon>
          {{ 'advisor.home.nothingWaiting' | transloco }}
        </p>
      </section>

      <section class="liwa-card" aria-labelledby="advisees-heading" [attr.aria-busy]="students.isLoading()">
        <h2 id="advisees-heading" class="card-title">
          <mat-icon aria-hidden="true">groups</mat-icon>
          {{ 'advisor.home.advisees' | transloco }}
          @if (students.hasValue() && students.value().length > 0) {
            <span class="count">{{ 'advisor.home.students.count' | transloco: { count: students.value().length } }}</span>
          }
        </h2>

        @if (students.hasValue()) {
          @if (students.value().length === 0) {
            <p class="liwa-muted">{{ 'advisor.home.students.none' | transloco }}</p>
          } @else {
            <ul class="students">
              @for (s of students.value(); track s.userId) {
                <li>
                  <div class="who">
                    <strong>{{ s.displayName }}</strong>
                    <span class="liwa-muted">{{ program(s) }}</span>
                  </div>
                  <div class="numbers">
                    <span>{{ 'advisor.home.students.credits' | transloco: { earned: s.creditsEarned, required: s.creditsRequired } }}</span>
                    <span class="liwa-muted">
                      {{ s.gpa === null ? ('advisor.home.students.noGpa' | transloco) : ('advisor.home.students.gpa' | transloco: { gpa: gpa(s.gpa) }) }}
                    </span>
                    @if (s.onProbation) {
                      <span class="tag">{{ 'advisor.home.students.probation' | transloco }}</span>
                    }
                  </div>
                </li>
              }
            </ul>
          }
        } @else if (students.error()) {
          <p class="liwa-muted">{{ 'advisor.home.students.error' | transloco }}</p>
        } @else {
          <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
        }
      </section>
    </div>
  `,
  styleUrls: ['../../shared/page.scss', './advisor-home.page.scss'],
})
export class AdvisorHomePage {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);

  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
  protected readonly students = resource({ loader: () => firstValueFrom(this.http.get<AdviseeSummary[]>('/api/advisor/students')) });
  protected readonly gpa = formatGpa;

  protected program(s: AdviseeSummary): string {
    return programName(s, this.language.lang());
  }
}
