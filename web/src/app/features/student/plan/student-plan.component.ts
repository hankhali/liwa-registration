import { Component, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import {
  BlockedGroup,
  CourseAssessment,
  Day,
  DeliveryPreference,
  PERIODS,
  PRESETS,
  PresetKey,
  Reason,
  SENT_STATUSES,
  STUDENT_PLAN_REASONS,
  SectionInfo,
  TEACHING_DAYS,
  blockedGroup,
  courseTitle,
  formatTime,
  meetingPatterns,
  parseTermCode,
  timeRange,
  warningKey,
  windowsFromGrid,
} from '../../../core/planning/planning.models';
import { PlanningService } from '../../../core/planning/planning.service';
import { formatDateTime } from '../../../core/academic/academic.models';
import { ReasonTextPipe } from '../../../core/planning/reason-text.pipe';

interface PlannedRow {
  code: string;
  title: string;
  credits: number;
  section: SectionInfo | undefined;
  reasons: Reason[];
}

/**
 * The student's plan (brief §9): shows the recommended plan straight away, lets the student say when
 * they're free with big preset cards, change courses with live checks, and send the plan to their
 * advisor after confirming each remaining warning.
 */
@Component({
  selector: 'app-student-plan',
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatIconModule, MatProgressSpinnerModule, MatSelectModule, TranslocoPipe, ReasonTextPipe],
  templateUrl: './student-plan.component.html',
  styleUrls: ['../../../shared/page.scss', './student-plan.component.scss'],
})
export class StudentPlanComponent {
  private readonly planning = inject(PlanningService);
  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly presets: PresetKey[] = ['any', 'evenings', 'mornings', 'daytime', 'custom'];
  protected readonly presetIcons: Record<PresetKey, string> = {
    any: 'all_inclusive',
    evenings: 'nights_stay',
    mornings: 'wb_sunny',
    daytime: 'light_mode',
    custom: 'tune',
  };
  protected readonly days = TEACHING_DAYS;
  protected readonly periods = PERIODS;
  protected readonly blockedOrder: BlockedGroup[] = ['prerequisite', 'notOffered', 'moreCredits', 'permission', 'other'];

  // ---- preferences
  protected readonly preset = signal<PresetKey>('any');
  protected readonly grid = signal<Record<string, boolean>>({});
  protected readonly delivery = signal<DeliveryPreference>('Any');
  protected readonly maxCredits = signal<number | null>(null);
  protected readonly availability = computed(() => {
    const p = this.preset();
    return p === 'custom' ? windowsFromGrid(this.grid()) : PRESETS[p];
  });

  // ---- data
  protected readonly view = resource({
    params: () => ({ availability: this.availability(), delivery: this.delivery(), maxCredits: this.maxCredits() }),
    loader: ({ params }) => this.planning.view(params.availability, params.delivery, params.maxCredits),
  });

  /** courseCode → sectionId currently in the plan. */
  protected readonly selection = signal<Record<string, string>>({});
  protected readonly edited = signal(false);
  protected readonly editing = signal(false);
  protected readonly confirming = signal(false);
  protected readonly acknowledged = signal<Record<string, boolean>>({});
  protected readonly busy = signal(false);
  protected readonly message = signal<'sent' | 'saved' | 'accepted' | 'error' | null>(null);

  protected readonly check = resource({
    params: () => (this.view.hasValue() ? { ids: Object.values(this.selection()), availability: this.availability() } : undefined),
    loader: ({ params }) => this.planning.check(params.ids, params.availability),
  });

  protected readonly sent = computed(() => {
    const saved = this.view.hasValue() ? this.view.value().saved : null;
    return saved !== null && SENT_STATUSES.includes(saved.status);
  });

  /** The advisor's reply, when the plan came back (sent back or changes proposed). */
  protected readonly reply = computed(() => {
    const v = this.view.value();
    const status = v?.saved?.status;
    if (!v || (status !== 'Rejected' && status !== 'ChangesProposed')) return null;
    const last = [...v.history].reverse().find((e) => e.actorRole === 'Advisor' && (e.action === 'rejected' || e.action === 'proposed'));
    return { status, note: last?.note ?? null, advisor: last?.actorName ?? v.advisorName };
  });

  protected readonly history = computed(() => [...(this.view.value()?.history ?? [])].reverse());

  protected async accept(): Promise<void> {
    this.busy.set(true);
    try {
      await this.planning.accept();
      this.message.set('accepted');
      this.view.reload();
    } catch {
      this.message.set('error');
    } finally {
      this.busy.set(false);
    }
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  private readonly sectionById = computed(() => new Map((this.view.hasValue() ? this.view.value().sections : []).map((s) => [s.id, s])));
  private readonly courseByCode = computed(() => new Map((this.view.hasValue() ? this.view.value().courses : []).map((c) => [c.code, c])));

  constructor() {
    // When a new recommendation arrives, start from the saved plan (if any) or the recommendation.
    effect(() => {
      if (!this.view.hasValue()) return;
      const v = this.view.value();
      untracked(() => {
        const saved = v.saved;
        const useSaved = saved && (SENT_STATUSES.includes(saved.status) || !this.edited());
        const ids = useSaved ? saved.sectionIds : v.recommendation.plan.map((p) => p.sectionId);
        const next: Record<string, string> = {};
        for (const id of ids) {
          const s = v.sections.find((x) => x.id === id);
          if (s) next[s.courseCode] = s.id;
        }
        this.selection.set(next);
        this.edited.set(false);
      });
    });
  }

  // ---------------------------------------------------------------- derived view data

  protected readonly rows = computed<PlannedRow[]>(() => {
    if (!this.view.hasValue()) return [];
    const v = this.view.value();
    const lang = this.language.lang();
    return Object.entries(this.selection())
      .map(([code, sectionId]) => {
        const course = this.courseByCode().get(code);
        const recommended = v.recommendation.plan.find((p) => p.courseCode === code);
        const assessed = v.recommendation.courses.find((c) => c.courseCode === code);
        return {
          code,
          title: courseTitle(course, lang),
          credits: course?.credits ?? 0,
          section: this.sectionById().get(sectionId),
          reasons: (recommended?.reasons ?? assessed?.reasons ?? []).filter((r) => STUDENT_PLAN_REASONS.includes(r.code)),
        };
      })
      .sort((a, b) => this.rank(a.code) - this.rank(b.code) || a.code.localeCompare(b.code));
  });

  protected readonly totalCredits = computed(() => this.rows().reduce((sum, r) => sum + r.credits, 0));

  /** "Your week": classes per teaching day, in time order; online classes listed separately. */
  protected readonly week = computed(() => {
    const lang = this.language.lang();
    const byDay = new Map<Day, { code: string; title: string; time: string; start: string }[]>();
    const online: { code: string; title: string }[] = [];
    for (const r of this.rows()) {
      if (!r.section || r.section.meetings.length === 0) {
        online.push({ code: r.code, title: r.title });
        continue;
      }
      for (const m of r.section.meetings) {
        const list = byDay.get(m.day) ?? [];
        list.push({ code: r.code, title: r.title, time: timeRange(m, lang), start: m.start });
        byDay.set(m.day, list);
      }
    }
    const days = TEACHING_DAYS.concat(['Saturday'] as Day[])
      .filter((d) => d !== 'Saturday' || byDay.has(d))
      .map((day) => ({ day, classes: (byDay.get(day) ?? []).sort((a, b) => a.start.localeCompare(b.start)) }));
    return { days, online };
  });

  /** Courses the student could add (eligible now or with a condition), best first. */
  protected readonly candidates = computed<CourseAssessment[]>(() => {
    if (!this.view.hasValue()) return [];
    return this.view.value().recommendation.courses.filter((c) => c.status !== 'NotEligible' && this.sectionsFor(c.courseCode).length > 0);
  });

  protected readonly blocked = computed(() => {
    if (!this.view.hasValue()) return [];
    const groups = new Map<BlockedGroup, CourseAssessment[]>();
    for (const c of this.view.value().recommendation.courses.filter((x) => x.status === 'NotEligible')) {
      const g = blockedGroup(c.reasons);
      groups.set(g, [...(groups.get(g) ?? []), c]);
    }
    return this.blockedOrder.filter((g) => groups.has(g)).map((g) => ({ group: g, courses: groups.get(g)! }));
  });

  protected readonly warnings = computed(() => (this.check.hasValue() ? this.check.value().warnings : []));
  protected readonly errors = computed(() => (this.check.hasValue() ? this.check.value().errors : []));
  protected readonly allConfirmed = computed(() => this.warnings().every((w) => this.acknowledged()[warningKey(w)]));
  protected readonly canSend = computed(() => this.rows().length > 0 && this.check.hasValue() && this.errors().length === 0 && !this.check.isLoading());

  protected readonly graduation = computed(() => {
    if (!this.view.hasValue()) return null;
    const p = this.view.value().recommendation.projection;
    return { regular: p.withoutSummer.graduationTerm, summer: p.withSummer.graduationTerm };
  });

  // ---------------------------------------------------------------- actions

  protected choosePreset(key: PresetKey): void {
    this.preset.set(key);
    this.message.set(null);
  }

  protected toggleCell(day: Day, period: string): void {
    this.grid.update((g) => ({ ...g, [`${day}.${period}`]: !g[`${day}.${period}`] }));
  }

  protected cellAllowed(day: Day, period: string): boolean {
    return !(day === 'Friday' && period !== 'morning');
  }

  protected toggleCourse(code: string, checked: boolean): void {
    this.selection.update((sel) => {
      const next = { ...sel };
      if (!checked) delete next[code];
      else {
        const recommended = this.view.value()?.recommendation.plan.find((p) => p.courseCode === code)?.sectionId;
        const options = this.sectionsFor(code);
        next[code] = recommended ?? [...options].sort((a, b) => b.seatsLeft - a.seatsLeft)[0]?.id;
      }
      return next;
    });
    this.edited.set(true);
    this.confirming.set(false);
  }

  protected chooseSection(code: string, sectionId: string): void {
    this.selection.update((sel) => ({ ...sel, [code]: sectionId }));
    this.edited.set(true);
    this.confirming.set(false);
  }

  protected useRecommendation(): void {
    this.edited.set(false);
    this.view.reload();
  }

  protected startSend(): void {
    this.acknowledged.set({});
    this.confirming.set(true);
  }

  protected setAcknowledged(r: Reason, value: boolean): void {
    this.acknowledged.update((a) => ({ ...a, [warningKey(r)]: value }));
  }

  protected async save(submit: boolean): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      const keys = this.warnings().map(warningKey);
      const res = await this.planning.save(Object.values(this.selection()), this.availability(), submit, submit ? keys : []);
      if (res.outcome === 'Saved') {
        this.message.set(submit ? 'sent' : 'saved');
        this.confirming.set(false);
        this.editing.set(false);
        this.edited.set(!submit);
        this.view.reload();
      } else {
        this.message.set('error');
        this.check.reload();
      }
    } catch {
      this.message.set('error');
    } finally {
      this.busy.set(false);
    }
  }

  // ---------------------------------------------------------------- template helpers

  protected sectionsFor(code: string): SectionInfo[] {
    return this.view.hasValue() ? this.view.value().sections.filter((s) => s.courseCode === code) : [];
  }

  protected title(code: string): string {
    return courseTitle(this.courseByCode().get(code), this.language.lang());
  }

  protected patterns(s: SectionInfo | undefined) {
    return s ? meetingPatterns(s.meetings) : [];
  }

  protected range(start: string, end: string): string {
    const lang = this.language.lang();
    return `${formatTime(start, lang)}–${formatTime(end, lang)}`;
  }

  protected termName(code: string | null): { season: string; year: number } | null {
    return code ? parseTermCode(code) : null;
  }

  /** "2027SP" → "Spring 2027" / "الربيع 2027". */
  protected termLabel(code: string): string {
    const t = parseTermCode(code);
    return t ? `${this.transloco.translate('planner.season.' + t.season)} ${t.year}` : code;
  }

  protected key(r: Reason): string {
    return warningKey(r);
  }

  private rank(code: string): number {
    return this.view.value()?.recommendation.courses.find((c) => c.courseCode === code)?.priorityRank ?? 999;
  }
}
