import { HttpClient } from '@angular/common/http';
import { Component, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { AdviseeSummary, formatDateTime, formatGpa, programName } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { parseTermCode } from '../../core/planning/planning.models';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { StatusChipComponent } from '../../shared/status-chip.component';

/** The advisor's students (brief §10): standing, GPA, plan status, projected graduation, last activity. */
@Component({
  selector: 'app-advisees-page',
  imports: [RouterLink, TranslocoPipe, EmptyStateComponent, PageHeaderComponent, StatusChipComponent],
  template: `
    <app-page-header [heading]="'advisor.advisees.heading' | transloco" [subtitle]="'advisor.advisees.subtitle' | transloco" />

    @if (students.hasValue()) {
      @if (students.value().length === 0) {
        <app-empty-state icon="groups" [heading]="'advisor.home.students.none' | transloco" />
      } @else {
        <div class="liwa-card table-card">
          <table class="liwa-table advisees">
            <thead>
              <tr>
                <th scope="col">{{ 'advisor.queue.cols.student' | transloco }}</th>
                <th scope="col">{{ 'advisor.advisees.cols.standing' | transloco }}</th>
                <th scope="col">{{ 'advisor.advisees.cols.credits' | transloco }}</th>
                <th scope="col">{{ 'advisor.advisees.cols.gpa' | transloco }}</th>
                <th scope="col">{{ 'advisor.advisees.cols.plan' | transloco }}</th>
                <th scope="col">{{ 'advisor.advisees.cols.graduation' | transloco }}</th>
                <th scope="col">{{ 'advisor.advisees.cols.activity' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (s of students.value(); track s.userId) {
                <tr>
                  <td>
                    <a [routerLink]="['/advisor/students', s.userId]" class="who">{{ s.displayName }}</a>
                    <span class="sub"><bdi dir="ltr">{{ s.studentId }}</bdi> · {{ program(s) }}</span>
                    <span class="markers">
                      @if (s.onProbation) {
                        <span class="marker">{{ 'advisor.queue.markers.probation' | transloco }}</span>
                      }
                      @if (s.hold) {
                        <span class="marker">{{ 'advisor.queue.markers.hold' | transloco }}</span>
                      }
                    </span>
                  </td>
                  <td data-label>{{ s.standing ? ('student.home.studies.standing.' + s.standing | transloco) : '—' }}</td>
                  <td class="num">{{ 'advisor.home.students.credits' | transloco: { earned: s.creditsEarned, required: s.creditsRequired } }}</td>
                  <td class="num" dir="ltr">{{ s.gpa === null ? '—' : gpa(s.gpa) }}</td>
                  <td>
                    @if (s.planStatus) {
                      @if (s.planId && s.planStatus !== 'Draft') {
                        <a [routerLink]="['/advisor/plans', s.planId]"><app-status-chip [status]="s.planStatus" /></a>
                      } @else {
                        <app-status-chip [status]="s.planStatus" />
                      }
                    } @else {
                      <span class="liwa-muted">{{ 'advisor.advisees.noPlan' | transloco }}</span>
                    }
                  </td>
                  <td>{{ term(s.projectedGraduation) }}</td>
                  <td class="sub">{{ s.lastActivity ? when(s.lastActivity) : '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    } @else if (students.error()) {
      <p class="liwa-muted">{{ 'advisor.home.students.error' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    .table-card {
      padding: 0.5rem;
      overflow-x: auto;
    }
    .who {
      font-weight: 600;
    }
    .sub {
      display: block;
      color: var(--liwa-text-muted);
      font-size: 0.8125rem;
    }
    .num {
      white-space: nowrap;
      font-variant-numeric: tabular-nums;
    }
    .markers {
      display: flex;
      gap: 0.375rem;
      margin-top: 0.25rem;
    }
    .marker {
      font-size: 0.75rem;
      font-weight: 600;
      padding: 0 0.375rem;
      border-radius: 4px;
      background: var(--liwa-chip-needs-bg);
      color: var(--liwa-chip-needs-fg);
    }
    @media (max-width: 760px) {
      thead {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
      }
      tr {
        display: grid;
        grid-template-columns: 1fr 1fr;
        padding: 0.5rem 0;
        border-bottom: 1px solid var(--liwa-border);
      }
      td {
        border: 0 !important;
        padding: 0.125rem 0.5rem !important;
      }
      td:first-child {
        grid-column: 1 / -1;
      }
    }
  `,
})
export class AdviseesPage {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly students = resource({ loader: () => firstValueFrom(this.http.get<AdviseeSummary[]>('/api/advisor/students')) });
  protected readonly gpa = formatGpa;

  protected program(s: AdviseeSummary): string {
    return programName(s, this.language.lang());
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  protected term(code: string | null | undefined): string {
    const t = code ? parseTermCode(code) : null;
    return t ? `${this.transloco.translate('planner.season.' + t.season)} ${t.year}` : '—';
  }
}
