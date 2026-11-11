import { Component, computed, inject, input, output } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../core/i18n/language.service';
import {
  PlanningView,
  Reason,
  SectionInfo,
  TEACHING_DAYS,
  TimeWindow,
  courseTitle,
  criticalCodes,
  meetingPatterns,
  timeRange,
} from '../core/planning/planning.models';
import { ReasonTextPipe } from '../core/planning/reason-text.pipe';
import { TimetableBlock, TimetableGridComponent } from './timetable-grid.component';

const PLAN_REASON_ORDER = ['TakeWithCorequisite', 'WaitingForGrade', 'OnlyOfferedIn', 'UnlocksOtherCourses', 'CountsTowardElectives', 'RequiredForDegree'];

/** The one reason worth reading about why a class can go in the plan. */
export function editorReason(reasons: Reason[]): Reason {
  for (const code of PLAN_REASON_ORDER) {
    const r = reasons.find((x) => x.code === code);
    if (r) return r;
  }
  return reasons[0] ?? { code: 'RequiredForDegree', courses: [], value: null };
}

/**
 * "Change courses" — the two-panel planner (brief §9): on the left every class the student can take this semester,
 * grouped by requirement, each with code, title, credits, the gold "Take this soon" badge, one plain reason and a
 * class-time picker; on the right the week with clashes and a credit counter against the limits.
 *
 * The same component is the student's planner and the advisor's "Suggest changes" (brief §10: "opens the same
 * planner the student uses, run on the student's record"). It only edits a selection (course → class); the page
 * around it checks and saves.
 */
@Component({
  selector: 'app-plan-editor',
  imports: [MatCheckboxModule, MatIconModule, MatSelectModule, TranslocoPipe, ReasonTextPipe, TimetableGridComponent],
  template: `
    <div class="editor">
      <div class="editor-left">
        <h3>{{ 'plan.editor.canTake' | transloco }}</h3>
        @for (g of candidateGroups(); track g.key) {
          <section class="group" [attr.aria-labelledby]="idPrefix() + 'grp-' + g.key">
            <h4 [id]="idPrefix() + 'grp-' + g.key">{{ g.name }}</h4>
            <ul class="candidates">
              @for (c of g.courses; track c.courseCode) {
                <li>
                  <div class="cand-head">
                    <mat-checkbox [checked]="!!selection()[c.courseCode]" (change)="toggleCourse(c.courseCode, $event.checked)">
                      <bdi class="code" dir="ltr">{{ c.courseCode }}</bdi>&nbsp;{{ title(c.courseCode) }}
                      <span class="muted">· {{ (c.credits === 1 ? 'plan.credit1' : 'plan.credits') | transloco: { n: c.credits } }}</span>
                    </mat-checkbox>
                    @if (critical().has(c.courseCode)) {
                      <span class="liwa-critical"><mat-icon aria-hidden="true" class="inline-icon">star</mat-icon>{{ 'plan.critical' | transloco }}</span>
                    }
                  </div>
                  <p class="why-one muted">{{ reason(c.reasons) | reasonText }}</p>
                  @if (selection()[c.courseCode]) {
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="section-pick">
                      <mat-label>{{ 'plan.editor.classTime' | transloco }}</mat-label>
                      <mat-select [value]="selection()[c.courseCode]" (valueChange)="chooseSection(c.courseCode, $event)">
                        @for (s of sectionsFor(c.courseCode); track s.id) {
                          <mat-option [value]="s.id" [disabled]="s.seatsLeft <= 0">
                            {{ sectionLabel(s) }}@if (s.seatsLeft <= 0) { · {{ 'plan.editor.full' | transloco }}}
                          </mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  }
                </li>
              }
            </ul>
          </section>
        }
      </div>
      <div class="editor-right">
        <div class="counter" [class.over]="limits() && totalCredits() > limits()!.max" aria-live="polite">
          <strong>{{ 'plan.editor.counter' | transloco: { n: totalCredits() } }}</strong>
          @if (limits(); as l) {
            <span>{{ 'plan.editor.counterLimits' | transloco: { min: l.min ?? 0, max: l.max } }}</span>
          }
        </div>
        <app-timetable-grid [blocks]="blocks()" [online]="online()" [days]="gridDays()" [free]="availability()" />
      </div>
    </div>
  `,
  styles: `
    .editor {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr);
      gap: 1.5rem;
    }
    .editor-right {
      position: sticky;
      top: 80px;
      align-self: start;
    }
    .group h4 {
      margin: 1rem 0 0.25rem;
      font-size: 0.9375rem;
      color: var(--liwa-text-muted);
    }
    .candidates {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .candidates li {
      padding: 0.375rem 0;
      border-bottom: 1px solid var(--liwa-border);
    }
    .cand-head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.25rem 0.5rem;
    }
    .code {
      font-weight: 700;
      color: var(--liwa-primary);
    }
    .muted {
      color: var(--liwa-text-muted);
    }
    .why-one {
      margin: 0 0 0.25rem 2.5rem;
      font-size: 0.875rem;
    }
    .inline-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: -3px;
    }
    .section-pick {
      width: min(100%, 360px);
      margin-inline-start: 2.5rem;
      margin-bottom: 0.25rem;
    }
    .counter {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem 0.75rem;
      align-items: baseline;
      padding: 0.625rem 0.875rem;
      margin-bottom: 0.75rem;
      border-radius: var(--liwa-radius);
      background: var(--liwa-block);
    }
    .counter.over {
      background: var(--liwa-clash);
      color: var(--liwa-danger);
    }
    @media (max-width: 900px) {
      .editor {
        grid-template-columns: 1fr;
      }
      .editor-right {
        position: static;
      }
      .section-pick,
      .why-one {
        margin-inline-start: 0;
      }
    }
  `,
})
export class PlanEditorComponent {
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);

  /** The student's planning view (recommendation, courses, class times). */
  readonly view = input.required<PlanningView>();
  /** courseCode → sectionId. */
  readonly selection = input.required<Record<string, string>>();
  readonly availability = input<TimeWindow[]>([]);
  readonly limits = input<{ min: number | null; max: number } | null>(null);
  /** Keeps element ids unique when two editors could be on one page. */
  readonly idPrefix = input('ed-');
  readonly selectionChange = output<Record<string, string>>();

  protected readonly critical = computed(() => criticalCodes(this.view().recommendation.courses));
  private readonly courseByCode = computed(() => new Map(this.view().courses.map((c) => [c.code, c])));
  private readonly sectionById = computed(() => new Map(this.view().sections.map((s) => [s.id, s])));

  /** Classes that could be added (can take now, or with a condition), grouped by requirement group. */
  protected readonly candidateGroups = computed(() => {
    const v = this.view();
    const candidates = v.recommendation.courses.filter((c) => c.status !== 'NotEligible' && this.sectionsFor(c.courseCode).length > 0);
    const groups = v.groups ?? [];
    const order = (k: string) => {
      const i = groups.findIndex((g) => g.key === k);
      return i < 0 ? 999 : i;
    };
    const keys = [...new Set(candidates.map((c) => c.groupKey))].sort((a, b) => order(a) - order(b));
    return keys.map((k) => ({
      key: k,
      name: groups.find((g) => g.key === k)?.name ?? k,
      courses: candidates.filter((c) => c.groupKey === k).sort((a, b) => a.priorityRank - b.priorityRank),
    }));
  });

  protected readonly totalCredits = computed(() =>
    Object.keys(this.selection()).reduce((n, code) => n + (this.courseByCode().get(code)?.credits ?? 0), 0),
  );

  protected readonly blocks = computed<TimetableBlock[]>(() => {
    const lang = this.language.lang();
    return Object.entries(this.selection()).flatMap(([code, id]) =>
      (this.sectionById().get(id)?.meetings ?? []).map((m, i) => ({
        key: `${code}-${i}`,
        code,
        label: courseTitle(this.courseByCode().get(code), lang),
        day: m.day,
        start: m.start,
        end: m.end,
      })),
    );
  });
  protected readonly online = computed(() => {
    const lang = this.language.lang();
    return Object.entries(this.selection())
      .filter(([, id]) => this.sectionById().get(id)?.meetings.length === 0)
      .map(([code]) => ({ code, label: courseTitle(this.courseByCode().get(code), lang) }));
  });
  protected readonly gridDays = computed(() => (this.blocks().some((b) => b.day === 'Saturday') ? [...TEACHING_DAYS, 'Saturday' as const] : TEACHING_DAYS));

  protected readonly reason = editorReason;

  protected toggleCourse(code: string, checked: boolean): void {
    const next = { ...this.selection() };
    if (!checked) delete next[code];
    else {
      const recommended = this.view().recommendation.plan.find((p) => p.courseCode === code)?.sectionId;
      const options = this.sectionsFor(code).filter((s) => s.seatsLeft > 0);
      const pick = recommended ?? [...options].sort((a, b) => b.seatsLeft - a.seatsLeft)[0]?.id;
      if (!pick) return;
      next[code] = pick;
    }
    this.selectionChange.emit(next);
  }

  protected chooseSection(code: string, sectionId: string): void {
    this.selectionChange.emit({ ...this.selection(), [code]: sectionId });
  }

  protected sectionsFor(code: string): SectionInfo[] {
    return this.view().sections.filter((s) => s.courseCode === code);
  }

  protected title(code: string): string {
    return courseTitle(this.courseByCode().get(code), this.language.lang());
  }

  /** "Mon & Wed 5:00 pm–6:15 pm" (never a class code like "02"). */
  protected sectionLabel(s: SectionInfo): string {
    const lang = this.language.lang();
    if (s.meetings.length === 0) return this.transloco.translate('plan.onlineAnyTime');
    return meetingPatterns(s.meetings)
      .map((p) => `${p.days.map((d) => this.transloco.translate('daysShort.' + d)).join(this.transloco.translate('plan.dayJoin'))} ${timeRange({ day: p.days[0], start: p.start, end: p.end }, lang)}`)
      .join(', ');
  }
}
