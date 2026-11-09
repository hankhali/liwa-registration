import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { AdviseeSummary, formatDateTime, formatGpa, programName } from '../../core/academic/academic.models';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { DECIDABLE, QueueItem } from '../../core/planning/advisor.models';
import { greetingName } from '../../shared/first-name';

/** Advisor home: plans that need attention first, then the advisor's students. */
@Component({
  selector: 'app-advisor-home',
  imports: [RouterLink, TranslocoPipe],
  template: `
    <header class="page-header">
      <h1>{{ 'advisor.home.welcome' | transloco: { name: name() } }}</h1>
      <p class="liwa-muted">{{ 'advisor.home.subtitle' | transloco }}</p>
    </header>

    <section class="block" aria-labelledby="attention-heading">
      <div class="block-head">
        <h2 id="attention-heading">{{ 'advisor.home.needsAttention' | transloco }}</h2>
        <a routerLink="/advisor/plans">{{ 'advisor.home.allPlans' | transloco }}</a>
      </div>
      @if (attention().length === 0) {
        <p class="quiet">{{ 'advisor.home.nothingWaiting' | transloco }}</p>
      } @else {
        <ul class="rows">
          @for (q of attention(); track q.planId) {
            <li [class.urgent]="q.urgent">
              <a [routerLink]="['/advisor/plans', q.planId]" class="who">{{ q.studentName }}</a>
              <span class="what">{{ 'planStatus.' + q.status | transloco }} · {{ 'plan.credits' | transloco: { n: q.credits } }}</span>
              <span class="when">
                @if (q.urgent) {
                  <strong class="flag">{{ 'advisor.queue.urgent' | transloco }}</strong>
                }
                {{ when(q.updatedAt) }}
              </span>
            </li>
          }
        </ul>
      }
    </section>

    <section class="block" aria-labelledby="students-heading">
      <div class="block-head">
        <h2 id="students-heading">{{ 'advisor.home.advisees' | transloco }}</h2>
        @if (students.hasValue()) {
          <span class="liwa-muted">{{ 'advisor.home.students.count' | transloco: { count: students.value().length } }}</span>
        }
      </div>
      @if (students.hasValue()) {
        @if (students.value().length === 0) {
          <p class="quiet">{{ 'advisor.home.students.none' | transloco }}</p>
        } @else {
          <table class="students">
            <tbody>
              @for (s of students.value(); track s.userId) {
                <tr>
                  <td>
                    <a [routerLink]="['/advisor/students', s.userId]" class="who">{{ s.displayName }}</a>
                    <span class="sub">{{ program(s) }}</span>
                  </td>
                  <td class="num">{{ 'advisor.home.students.credits' | transloco: { earned: s.creditsEarned, required: s.creditsRequired } }}</td>
                  <td class="num">
                    {{ s.gpa === null ? ('advisor.home.students.noGpa' | transloco) : ('advisor.home.students.gpa' | transloco: { gpa: gpa(s.gpa) }) }}
                  </td>
                  <td>
                    @if (planStatus(s.userId); as st) {
                      <span class="sub">{{ 'planStatus.' + st | transloco }}</span>
                    }
                    @if (s.onProbation) {
                      <span class="flag">{{ 'advisor.home.students.probation' | transloco }}</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      } @else if (students.error()) {
        <p class="quiet">{{ 'advisor.home.students.error' | transloco }}</p>
      } @else {
        <p class="quiet">{{ 'common.loading' | transloco }}</p>
      }
    </section>
  `,
  styleUrls: ['../../shared/page.scss', './advisor-home.page.scss'],
})
export class AdvisorHomePage {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);

  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
  protected readonly students = resource({ loader: () => firstValueFrom(this.http.get<AdviseeSummary[]>('/api/advisor/students')) });
  protected readonly queue = resource({ loader: () => firstValueFrom(this.http.get<QueueItem[]>('/api/advisor/plans')) });
  protected readonly attention = computed(() =>
    (this.queue.value() ?? [])
      .filter((q) => DECIDABLE.includes(q.status))
      .sort((a, b) => Number(b.urgent) - Number(a.urgent) || a.updatedAt.localeCompare(b.updatedAt))
      .slice(0, 6),
  );
  protected readonly gpa = formatGpa;

  protected program(s: AdviseeSummary): string {
    return programName(s, this.language.lang());
  }

  protected planStatus(userId: string): string | null {
    return (this.queue.value() ?? []).find((q) => q.studentUserId === userId)?.status ?? null;
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }
}
