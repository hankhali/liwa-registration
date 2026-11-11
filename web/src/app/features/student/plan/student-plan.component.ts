import { Component, DestroyRef, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { localDigits } from '../../../core/i18n/digits';
import { CourseTitles } from '../../../core/planning/course-titles';
import { LanguageService } from '../../../core/i18n/language.service';
import {
  AvailabilityPreset,
  BlockedGroup,
  CourseAssessment,
  PRESETS,
  Preferences,
  Reason,
  SENT_STATUSES,
  STUDENT_PLAN_REASONS,
  SectionInfo,
  TEACHING_DAYS,
  blockedGroup,
  courseTitle,
  criticalCodes,
  meetingPatterns,
  parseTermCode,
  termLabel,
  timeRange,
  warningKey,
} from '../../../core/planning/planning.models';
import { PlanningService } from '../../../core/planning/planning.service';
import { ReasonTextPipe } from '../../../core/planning/reason-text.pipe';
import { ExplainerPanelComponent } from '../../../shared/explainer-panel.component';
import { PageHeaderComponent } from '../../../shared/page-header.component';
import { PlanEditorComponent } from '../../../shared/plan-editor.component';
import { TimetableBlock, TimetableGridComponent } from '../../../shared/timetable-grid.component';

interface PlannedRow {
  code: string;
  title: string;
  credits: number;
  section: SectionInfo | undefined;
  /** The one most useful plain reason for this class (UX pass: one line, not a list). */
  reason: Reason | null;
  critical: boolean;
  rank: number | null;
  /** Other class times and why they weren't picked (labelled by day and time, never by section code). */
  alternatives: { sectionId: string; label: string; reasons: Reason[] }[];
}

/** Reasons that say why a class is in the plan; the first useful one is shown (RequiredForDegree only as a fallback). */
const PLAN_REASON_ORDER = ['TakeWithCorequisite', 'WaitingForGrade', 'OnlyOfferedIn', 'UnlocksOtherCourses', 'CountsTowardElectives', 'RequiredForDegree'];

/** Pick the reason a student most needs to read: why it's here, or why it has to wait. */
export function mainReason(reasons: Reason[]): Reason {
  for (const code of PLAN_REASON_ORDER) {
    const r = reasons.find((x) => x.code === code);
    if (r) return r;
  }
  return reasons[0] ?? { code: 'RequiredForDegree', courses: [], value: null };
}

const POSITIVE = new Set(['RequiredForDegree', 'CountsTowardElectives', 'SuggestedForThisSemester', 'UnlocksOtherCourses']);

/** The reason a class can't be taken now (skips "needed for your degree"-type reasons). */
export function blockingReason(reasons: Reason[]): Reason {
  return reasons.find((r) => !POSITIVE.has(r.code)) ?? reasons[0] ?? { code: 'NotEligibleNow', courses: [], value: null };
}

/** The free-time choices offered as one-tap buttons on the planner. */
export const QUICK_PRESETS: Exclude<AvailabilityPreset, 'custom'>[] = ['any', 'daytime', 'evenings', 'weekends'];

const AUTOSAVE_MS = 1200;

/**
 * The planner (brief §9). First visit: the recommended plan is already built, with the timetable and one primary
 * button, "Send to my advisor". "Change courses" opens the two-panel view (courses you can take, grouped by
 * requirement, on the left; the timetable with clashes and a credit counter on the right). Unsaved changes are
 * autosaved as a draft. Free time comes from the Availability page.
 */
@Component({
  selector: 'app-student-plan',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    RouterLink,
    TranslocoPipe,
    ReasonTextPipe,
    TimetableGridComponent,
    ExplainerPanelComponent,
    PageHeaderComponent,
    PlanEditorComponent,
  ],
  templateUrl: './student-plan.component.html',
  styleUrls: ['../../../shared/page.scss', './student-plan.component.scss'],
})
export class StudentPlanComponent {
  private readonly planning = inject(PlanningService);
  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly titles = inject(CourseTitles);

  protected readonly blockedOrder: BlockedGroup[] = ['prerequisite', 'notOffered', 'moreCredits', 'permission', 'other'];

  // ---- data: saved preferences first, then the plan built with them
  protected readonly prefs = resource({ loader: () => this.planning.preferences() });
  protected readonly view = resource({
    params: () => (this.prefs.hasValue() || this.prefs.error() ? this.prefsOrDefault() : undefined),
    loader: ({ params }) => this.planning.view(params.availability, params.delivery, params.maxCredits),
  });

  /** courseCode → sectionId currently in the plan. */
  protected readonly selection = signal<Record<string, string>>({});
  protected readonly edited = signal(false);
  protected readonly restoredDraft = signal(false);
  protected readonly editing = signal(false);
  protected readonly confirming = signal(false);
  protected readonly acknowledged = signal<Record<string, boolean>>({});
  protected readonly openDetails = signal<Record<string, boolean>>({});
  protected readonly openWhy = signal<Record<string, boolean>>({});
  protected readonly busy = signal(false);
  protected readonly message = signal<'sent' | 'saved' | 'accepted' | 'error' | null>(null);

  protected readonly availability = computed(() => this.prefsOrDefault().availability);
  protected readonly needsFreeTime = computed(() => this.prefs.hasValue() && this.prefs.value().preset === null);

  protected readonly check = resource({
    params: () => (this.view.hasValue() ? { ids: Object.values(this.selection()), availability: this.availability() } : undefined),
    loader: ({ params }) => this.planning.check(params.ids, params.availability),
  });

  protected readonly sent = computed(() => {
    const saved = this.view.hasValue() ? this.view.value().saved : null;
    return saved !== null && SENT_STATUSES.includes(saved.status);
  });

  /** The advisor's reply, when the plan came back (sent back, changes proposed, or needs review). */
  protected readonly reply = computed(() => {
    const v = this.view.value();
    const status = v?.saved?.status;
    if (!v || (status !== 'Rejected' && status !== 'ChangesProposed' && status !== 'NeedsReview')) return null;
    const last = [...v.history].reverse().find((e) => e.actorRole === 'Advisor' && (e.action === 'rejected' || e.action === 'proposed'));
    return { status, note: status === 'NeedsReview' ? null : (last?.note ?? null), advisor: last?.actorName ?? v.advisorName };
  });

  private readonly sectionById = computed(() => new Map((this.view.hasValue() ? this.view.value().sections : []).map((s) => [s.id, s])));
  private readonly courseByCode = computed(() => new Map((this.view.hasValue() ? this.view.value().courses : []).map((c) => [c.code, c])));
  private readonly critical = computed(() => criticalCodes(this.view.value()?.recommendation.courses ?? []));

  constructor() {
    // When a new recommendation arrives, start from the saved plan, then an autosaved draft, then the recommendation.
    effect(() => {
      if (!this.view.hasValue()) return;
      const v = this.view.value();
      untracked(() => {
        this.titles.register(v.courses);
        const saved = v.saved;
        const draft = this.prefs.value()?.draftSectionIds ?? [];
        const sentNow = !!saved && SENT_STATUSES.includes(saved.status);
        let ids: string[];
        if (sentNow) ids = saved!.sectionIds;
        else if (this.edited()) ids = Object.values(this.selection());
        else if (draft.length > 0 && draft.every((id) => v.sections.some((s) => s.id === id))) {
          ids = draft;
          this.restoredDraft.set(true);
        } else ids = saved ? saved.sectionIds : v.recommendation.plan.map((p) => p.sectionId);
        const next: Record<string, string> = {};
        for (const id of ids) {
          const s = v.sections.find((x) => x.id === id);
          if (s) next[s.courseCode] = s.id;
        }
        this.selection.set(next);
        this.edited.set(this.restoredDraft() && !sentNow);
      });
    });

    // Autosave: unsaved changes are kept as a draft (brief §9) — not a plan version, not sent to anyone.
    let timer: ReturnType<typeof setTimeout> | undefined;
    effect(() => {
      const ids = Object.values(this.selection());
      if (!this.edited() || this.sent()) return;
      clearTimeout(timer);
      timer = setTimeout(() => void this.planning.saveDraft(ids).catch(() => undefined), AUTOSAVE_MS);
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));
  }

  private prefsOrDefault(): Pick<Preferences, 'availability' | 'delivery' | 'maxCredits'> {
    const p = this.prefs.value();
    return { availability: p?.availability ?? [], delivery: p?.delivery ?? 'Any', maxCredits: p?.maxCredits ?? null };
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
        const reasons = (recommended?.reasons ?? assessed?.reasons ?? []).filter((r) => STUDENT_PLAN_REASONS.includes(r.code));
        return {
          code,
          title: courseTitle(course, lang),
          credits: course?.credits ?? 0,
          section: this.sectionById().get(sectionId),
          reason: reasons.length > 0 ? mainReason(reasons) : null,
          critical: this.critical().has(code),
          rank: assessed?.priorityRank ?? null,
          alternatives:
            recommended?.alternatives
              .filter((a) => this.sectionById().has(a.sectionId))
              .map((a) => ({ sectionId: a.sectionId, label: this.sectionLabel(this.sectionById().get(a.sectionId)!), reasons: a.reasons })) ?? [],
        };
      })
      .sort((a, b) => (a.rank ?? 999) - (b.rank ?? 999) || a.code.localeCompare(b.code));
  });

  protected readonly totalCredits = computed(() => this.rows().reduce((sum, r) => sum + r.credits, 0));
  protected readonly selectedIds = computed(() => Object.values(this.selection()));

  /** Classes on the timetable grid; online classes (no meetings) are listed under it. */
  protected readonly blocks = computed<TimetableBlock[]>(() =>
    this.rows().flatMap((r) => (r.section?.meetings ?? []).map((m, i) => ({ key: `${r.code}-${i}`, code: r.code, label: r.title, day: m.day, start: m.start, end: m.end }))),
  );
  protected readonly online = computed(() => this.rows().filter((r) => r.section && r.section.meetings.length === 0).map((r) => ({ code: r.code, label: r.title })));
  protected readonly gridDays = computed(() => (this.blocks().some((b) => b.day === 'Saturday') ? [...TEACHING_DAYS, 'Saturday' as const] : TEACHING_DAYS));

  protected readonly blocked = computed(() => {
    if (!this.view.hasValue()) return [];
    const groups = new Map<BlockedGroup, CourseAssessment[]>();
    for (const c of this.view.value().recommendation.courses.filter((x) => x.status === 'NotEligible')) {
      const g = blockedGroup(c.reasons);
      groups.set(g, [...(groups.get(g) ?? []), c]);
    }
    return this.blockedOrder.filter((g) => groups.has(g)).map((g) => ({ group: g, courses: groups.get(g)! }));
  });
  protected readonly blockedCount = computed(() => this.blocked().reduce((n, g) => n + g.courses.length, 0));

  protected readonly warnings = computed(() => (this.check.hasValue() ? this.check.value().warnings : []));
  protected readonly errors = computed(() => (this.check.hasValue() ? this.check.value().errors : []));
  protected readonly allConfirmed = computed(() => this.warnings().every((w) => this.acknowledged()[warningKey(w)]));
  protected readonly canSend = computed(() => this.rows().length > 0 && this.check.hasValue() && this.errors().length === 0 && !this.check.isLoading());
  protected readonly limits = computed(() => (this.check.hasValue() ? { min: this.check.value().limits?.minCredits ?? null, max: this.check.value().maxCredits } : null));

  protected readonly quickPresets = QUICK_PRESETS;
  protected readonly currentPreset = computed(() => this.prefs.value()?.preset ?? null);

  /** Page heading and first sentence (what to do here) for each state of the plan. */
  protected readonly firstLine = computed(() => {
    const status = this.view.value()?.saved?.status;
    if (status && status !== 'Draft') return 'studentStatus.meaning.' + status;
    return 'plan.intro';
  });

  protected readonly graduation = computed(() => {
    if (!this.view.hasValue()) return null;
    const p = this.view.value().recommendation.projection;
    return { regular: p.withoutSummer.graduationTerm, summer: p.withSummer.graduationTerm };
  });

  // ---------------------------------------------------------------- actions

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

  /** One tap on a free-time choice: save it and rebuild the plan for those times (drops any unsent changes). */
  protected async usePreset(p: Exclude<AvailabilityPreset, 'custom'>): Promise<void> {
    if (this.currentPreset() === p) return;
    this.busy.set(true);
    try {
      const prefs = this.prefsOrDefault();
      await this.planning.savePreferences(p, PRESETS[p].map((w) => ({ ...w })), prefs.delivery, prefs.maxCredits);
      await this.planning.saveDraft([]).catch(() => undefined);
      this.edited.set(false);
      this.restoredDraft.set(false);
      this.editing.set(false);
      this.confirming.set(false);
      this.prefs.reload();
    } catch {
      this.message.set('error');
    } finally {
      this.busy.set(false);
    }
  }

  /** A change in the two-panel planner: keep it (autosaved as a draft) and re-check. */
  protected onEditorChange(next: Record<string, string>): void {
    this.selection.set(next);
    this.edited.set(true);
    this.confirming.set(false);
  }

  protected async useRecommendation(): Promise<void> {
    this.edited.set(false);
    this.restoredDraft.set(false);
    await this.planning.saveDraft([]).catch(() => undefined);
    this.prefs.reload();
    this.view.reload();
  }

  protected startSend(): void {
    this.acknowledged.set({});
    this.confirming.set(true);
  }

  protected setAcknowledged(r: Reason, value: boolean): void {
    this.acknowledged.update((a) => ({ ...a, [warningKey(r)]: value }));
  }

  protected toggle(map: 'details' | 'why', code: string): void {
    const s = map === 'details' ? this.openDetails : this.openWhy;
    s.update((m) => ({ ...m, [code]: !m[code] }));
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
        this.edited.set(false);
        this.restoredDraft.set(false);
        await this.planning.saveDraft([]).catch(() => undefined);
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

  protected isCritical(code: string): boolean {
    return this.critical().has(code);
  }

  protected patterns(s: SectionInfo | undefined) {
    return s ? meetingPatterns(s.meetings) : [];
  }

  protected sectionLabel(s: SectionInfo): string {
    const lang = this.language.lang();
    if (s.meetings.length === 0) return this.transloco.translate('plan.onlineAnyTime');
    return meetingPatterns(s.meetings)
      .map((p) => `${p.days.map((d) => this.transloco.translate('daysShort.' + d)).join(this.transloco.translate('plan.dayJoin'))} ${timeRange({ day: p.days[0], start: p.start, end: p.end }, lang)}`)
      .join(', ');
  }

  protected range(start: string, end: string): string {
    return timeRange({ day: 'Monday', start, end }, this.language.lang());
  }

  /** "2027SP" → "Spring 2027" / "الربيع 2027". */
  protected termLabel(code: string): string {
    return termLabel(code, (k) => this.transloco.translate(k), this.language.lang());
  }

  protected key(r: Reason): string {
    return warningKey(r);
  }

  protected readonly mainReason = mainReason;
  protected readonly blockingReason = blockingReason;

  protected digits(n: number): string {
    return localDigits(String(n), this.language.lang());
  }
}
