import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, input, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { formatGpa, programName } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { StudentRecordView, TranscriptLine } from '../../core/planning/advisor.models';
import { courseTitle, parseTermCode } from '../../core/planning/planning.models';

/** Read-only student record with transcript, for the student's advisor. */
@Component({
  selector: 'app-student-record',
  imports: [RouterLink, TranslocoPipe],
  template: `
    <a class="back" routerLink="/advisor">← {{ 'advisor.record.back' | transloco }}</a>
    @if (record.hasValue()) {
      @let r = record.value();
      <header class="head">
        <h1>{{ r.studentName }}</h1>
        <p class="meta"><span dir="ltr">{{ r.summary.studentId }}</span> · {{ program() }}</p>
      </header>

      <dl class="facts">
        <div><dt>{{ 'advisor.review.creditsDone' | transloco }}</dt><dd>{{ r.summary.creditsEarned }} / {{ r.summary.creditsRequired }}</dd></div>
        <div><dt>{{ 'student.home.studies.gpa' | transloco }}</dt><dd dir="ltr">{{ r.summary.gpa === null ? '—' : gpa(r.summary.gpa) }}</dd></div>
        <div><dt>{{ 'student.home.studies.year' | transloco }}</dt><dd>{{ 'student.home.studies.standing.' + r.summary.standing | transloco }}</dd></div>
        <div>
          <dt>{{ 'advisor.record.plan' | transloco }}</dt>
          <dd>
            @if (r.currentPlanId && r.currentPlanStatus !== 'Draft') {
              <a [routerLink]="['/advisor/plans', r.currentPlanId]">{{ 'planStatus.' + r.currentPlanStatus | transloco }}</a>
            } @else {
              {{ 'advisor.record.noPlan' | transloco }}
            }
          </dd>
        </div>
      </dl>
      @if (r.summary.onProbation) {
        <p class="probation">{{ 'advisor.record.probation' | transloco }}</p>
      }

      <h2>{{ 'advisor.record.transcript' | transloco }}</h2>
      <table class="transcript">
        <thead>
          <tr>
            <th scope="col">{{ 'advisor.record.cols.term' | transloco }}</th>
            <th scope="col">{{ 'advisor.record.cols.course' | transloco }}</th>
            <th scope="col">{{ 'advisor.record.cols.credits' | transloco }}</th>
            <th scope="col">{{ 'advisor.record.cols.grade' | transloco }}</th>
          </tr>
        </thead>
        <tbody>
          @for (l of r.transcript; track $index) {
            <tr [class.fail]="l.grade === 'F'">
              <td>{{ termLabel(l.termCode) }}</td>
              <td><span class="code" dir="ltr">{{ l.courseCode }}</span> {{ title(l) }}</td>
              <td>{{ l.credits }}</td>
              <td>{{ gradeLabel(l) }}</td>
            </tr>
          }
        </tbody>
      </table>
    } @else if (record.error()) {
      <p class="muted">{{ 'advisor.record.notFound' | transloco }}</p>
    } @else {
      <p class="muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    .back { display: inline-block; margin-bottom: 1rem; text-decoration: none; }
    .head { margin-bottom: 1rem; h1 { margin: 0; } }
    .meta { margin: 0.25rem 0 0; color: var(--liwa-text-muted); }
    .muted { color: var(--liwa-text-muted); }
    .facts {
      display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 160px), 1fr)); gap: 0.75rem 1.5rem;
      margin: 0 0 1rem; padding: 1rem 0; border-top: 1px solid var(--liwa-border); border-bottom: 1px solid var(--liwa-border);
      dt { color: var(--liwa-text-muted); font-size: 0.8125rem; }
      dd { margin: 0; font-weight: 600; }
    }
    .probation { padding: 0.625rem 0.875rem; border-inline-start: 4px solid #b8892b; background: #fdf6e7; color: #6b4a00; }
    h2 { margin: 1.5rem 0 0.75rem; font-size: 1.0625rem; }
    .transcript {
      width: 100%; border-collapse: collapse; background: var(--liwa-surface); border: 1px solid var(--liwa-border); font-size: 0.9375rem;
      th { padding: 0.5rem 0.75rem; text-align: start; font-size: 0.8125rem; color: var(--liwa-text-muted); background: #fafaf8; border-bottom: 1px solid var(--liwa-border); }
      td { padding: 0.5rem 0.75rem; border-bottom: 1px solid var(--liwa-border); }
      tr.fail td { color: var(--liwa-danger); }
    }
    .code { font-weight: 700; margin-inline-end: 0.25rem; }
  `,
})
export class StudentRecordPage {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  readonly studentUserId = input.required<string>();
  protected readonly record = resource({
    params: () => this.studentUserId(),
    loader: ({ params }) => firstValueFrom(this.http.get<StudentRecordView>(`/api/advisor/students/${params}`)),
  });
  protected readonly program = computed(() => (this.record.value() ? programName(this.record.value()!.summary, this.language.lang()) : ''));
  protected readonly gpa = formatGpa;

  protected title(l: TranscriptLine): string {
    return courseTitle({ code: l.courseCode, title: l.title, titleAr: l.titleAr, credits: l.credits }, this.language.lang());
  }

  protected termLabel(code: string): string {
    const t = parseTermCode(code);
    return t ? `${this.transloco.translate('planner.season.' + t.season)} ${t.year}` : this.transloco.translate('advisor.record.transfer');
  }

  protected gradeLabel(l: TranscriptLine): string {
    if (l.status === 'InProgress') return this.transloco.translate('advisor.record.inProgress');
    if (l.status === 'Transfer') return this.transloco.translate('advisor.record.transferGrade');
    return l.grade ?? '';
  }
}
