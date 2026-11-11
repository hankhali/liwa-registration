import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, input, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { formatDateTime, formatGpa } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { DECIDABLE, PlanEvent, PlanReview } from '../../core/planning/advisor.models';
import {
  Day,
  PlanCheck,
  Reason,
  SectionInfo,
  TEACHING_DAYS,
  courseTitle,
  formatTime,
  meetingPatterns,
  parseTermCode, termLabel,
  timeRange,
} from '../../core/planning/planning.models';
import { ReasonTextPipe } from '../../core/planning/reason-text.pipe';
import { CourseTitles } from '../../core/planning/course-titles';
import { ExplainerPanelComponent } from '../../shared/explainer-panel.component';
import { PlanEditorComponent } from '../../shared/plan-editor.component';
import { PlanHistoryComponent } from '../../shared/plan-history.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { TimetableBlock, TimetableGridComponent } from '../../shared/timetable-grid.component';
import { semesterDelay } from '../../core/planning/planning.models';

type Mode = 'none' | 'reject' | 'propose';

/**
 * One-page review, no tabs (brief §10), in three columns: left = the student (credits, GPA, standing, limits,
 * probation, hold, free-time mini-grid); centre = the submitted classes on the timetable, each with the planner's
 * reasons (open by default), today's checks and the student's acknowledged warnings, and the history; right =
 * graduation for this plan vs the recommendation, with the delay. Bottom: approve (optional note; disabled while
 * checks show errors), send back (comment required) or suggest changes, which opens the same two-panel planner the
 * student uses, run on the student's record (note required; plans with errors can't be sent).
 */
@Component({
  selector: 'app-plan-review',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoPipe,
    ReasonTextPipe,
    PlanHistoryComponent,
    StatusChipComponent,
    TimetableGridComponent,
    ExplainerPanelComponent,
    PlanEditorComponent,
  ],
  templateUrl: './plan-review.page.html',
  styleUrls: ['../../shared/page.scss', './plan-review.page.scss'],
})
export class PlanReviewPage {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);
  private readonly titles = inject(CourseTitles);
  protected readonly language = inject(LanguageService);

  readonly planId = input.required<string>();

  protected readonly review = resource({
    params: () => this.planId(),
    loader: async ({ params }) => {
      const r = await firstValueFrom(this.http.get<PlanReview>(`/api/advisor/plans/${params}`));
      this.titles.register(r.planning.courses ?? []);
      return r;
    },
  });

  protected readonly mode = signal<Mode>('none');
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly done = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  /** courseCode → sectionId for a proposal. */
  protected readonly proposal = signal<Record<string, string>>({});
  protected readonly proposalCheck = signal<PlanCheck | null>(null);

  private readonly sections = computed(() => new Map((this.review.value()?.planning.sections ?? []).map((s) => [s.id, s])));

  protected readonly decidable = computed(() => DECIDABLE.includes(this.review.value()?.status ?? 'Draft'));
  protected readonly canApprove = computed(() => this.decidable() && (this.review.value()?.liveCheck.errors.length ?? 1) === 0 && !this.busy());

  /** The plan's classes, with the planner's recommendation details where the course was also recommended. */
  protected readonly courses = computed(() => {
    const r = this.review.value();
    if (!r) return [];
    const lang = this.language.lang();
    return r.sectionIds
      .map((id) => this.sections().get(id))
      .filter((s): s is SectionInfo => !!s)
      .map((s) => {
        const info = r.planning.courses.find((c) => c.code === s.courseCode);
        const rec = r.planning.recommendation.plan.find((p) => p.courseCode === s.courseCode);
        const assessed = r.planning.recommendation.courses.find((c) => c.courseCode === s.courseCode);
        return {
          code: s.courseCode,
          title: courseTitle(info, lang),
          credits: info?.credits ?? 0,
          section: s,
          recommended: !!rec,
          sameSectionAsRecommended: rec?.sectionId === s.id,
          rank: assessed?.priorityRank ?? null,
          unlocks: assessed?.unlocks ?? 0,
          chain: assessed?.chainLength ?? 0,
          reasons: rec?.reasons ?? assessed?.reasons ?? [],
          alternatives: rec?.alternatives ?? [],
        };
      })
      .sort((a, b) => (a.rank ?? 999) - (b.rank ?? 999));
  });

  protected readonly totalCredits = computed(() => this.courses().reduce((n, c) => n + c.credits, 0));

  protected readonly notInPlan = computed(() => {
    const r = this.review.value();
    if (!r) return [];
    const chosen = new Set(this.courses().map((c) => c.code));
    return r.planning.recommendation.plan.filter((p) => !chosen.has(p.courseCode)).map((p) => p.courseCode);
  });

  protected readonly week = computed(() => {
    const lang = this.language.lang();
    return TEACHING_DAYS.map((day) => ({
      day,
      classes: this.courses()
        .flatMap((c) => c.section.meetings.filter((m) => m.day === day).map((m) => ({ code: c.code, time: timeRange(m, lang), start: m.start })))
        .sort((a, b) => a.start.localeCompare(b.start)),
    })).filter((d) => d.classes.length > 0);
  });

  /** The submitted sections on the shared timetable grid. */
  protected readonly blocks = computed<TimetableBlock[]>(() =>
    this.courses().flatMap((c) => c.section.meetings.map((m, i) => ({ key: `${c.code}-${i}`, code: c.code, label: c.title, day: m.day, start: m.start, end: m.end }))),
  );
  protected readonly onlineBlocks = computed(() => this.courses().filter((c) => c.section.meetings.length === 0).map((c) => ({ code: c.code })));

  /** How many semesters this plan delays graduation compared with the recommendation (without summer). */
  protected readonly delta = computed(() => {
    const r = this.review.value();
    return r ? semesterDelay(r.planning.recommendation.projection.withoutSummer.graduationTerm, r.thisPlan.withoutSummer.graduationTerm, false) : 0;
  });

  // ---------------------------------------------------------------- actions

  protected startPropose(): void {
    const next: Record<string, string> = {};
    for (const c of this.courses()) next[c.code] = c.section.id;
    this.proposal.set(next);
    this.mode.set('propose');
    void this.checkProposal();
  }

  /** A change in the planner while suggesting changes: keep it and re-check it on the student's record. */
  protected setProposal(next: Record<string, string>): void {
    this.proposal.set(next);
    void this.checkProposal();
  }

  protected readonly proposalLimits = computed(() => {
    const pc = this.proposalCheck() ?? this.review.value()?.liveCheck;
    return pc ? { min: pc.limits?.minCredits ?? null, max: pc.maxCredits } : null;
  });
  protected async checkProposal(): Promise<void> {
    this.proposalCheck.set(
      await firstValueFrom(this.http.post<PlanCheck>(`/api/advisor/plans/${this.planId()}/check`, { sectionIds: Object.values(this.proposal()) })),
    );
  }

  protected async decide(decision: 'Approve' | 'Reject' | 'ProposeChanges'): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(
        this.http.post(`/api/advisor/plans/${this.planId()}/decision`, {
          decision,
          note: this.note().trim() || null,
          sectionIds: decision === 'ProposeChanges' ? Object.values(this.proposal()) : null,
        }),
      );
      this.done.set(decision);
      this.mode.set('none');
      this.note.set('');
      this.review.reload();
    } catch {
      this.error.set(decision);
    } finally {
      this.busy.set(false);
    }
  }

  // ---------------------------------------------------------------- helpers

  protected sectionsFor(code: string): SectionInfo[] {
    return (this.review.value()?.planning.sections ?? []).filter((s) => s.courseCode === code);
  }

  protected title(code: string): string {
    return courseTitle(this.review.value()?.planning.courses.find((c) => c.code === code), this.language.lang());
  }

  protected patterns(s: SectionInfo) {
    return meetingPatterns(s.meetings);
  }

  protected range(start: string, end: string): string {
    const lang = this.language.lang();
    return `${formatTime(start, lang)}–${formatTime(end, lang)}`;
  }

  protected term(code: string | null): string {
    return code && parseTermCode(code) ? termLabel(code, (k) => this.transloco.translate(k), this.language.lang()) : this.transloco.translate('advisor.review.unknownTerm');
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  protected gpa(v: number | null): string {
    return v === null ? '—' : formatGpa(v);
  }

  protected dayName(d: Day): string {
    return this.transloco.translate('days.' + d);
  }

  protected eventParams(e: PlanEvent): Record<string, unknown> {
    return { actor: e.actorName, version: e.version ?? '' };
  }

  protected advisorOnly(r: Reason): boolean {
    return r.code !== 'RequiredForDegree';
  }
}
