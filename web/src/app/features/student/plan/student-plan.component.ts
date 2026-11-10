import { Component, DestroyRef, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import {
  BlockedGroup,
  CourseAssessment,
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
  timeRange,
  warningKey,
} from '../../../core/planning/planning.models';
import { PlanningService } from '../../../core/planning/planning.service';
import { ReasonTextPipe } from '../../../core/planning/reason-text.pipe';
import { TimetableBlock, TimetableGridComponent } from '../../../shared/timetable-grid.component';

interface PlannedRow {
  code: string;
  title: string;
  credits: number;
  section: SectionInfo | undefined;
  reasons: Reason[];
  critical: boolean;
  rank: number | null;
  chain: number;
  unlocks: number;
  alternatives: { sectionCode: string; reasons: Reason[] }[];
}

const AUTOSAVE_MS = 1200;

/**
 * The planner (brief §9). First visit: the recommended plan is already built, with the timetable and one primary
 * button, "Send to my advisor". "Change courses" opens the two-panel view (courses you can take, grouped by
 * requirement, on the left; the timetable with clashes and a credit counter on the right). Unsaved changes are
 * autosaved as a draft. Free time comes from the Availability page.
 */
@Component({
  selector: 'app-student-plan',
  imports: [MatButtonModule, MatCheckboxModule, MatIconModule, MatProgressSpinnerModule, MatSelectModule, RouterLink, TranslocoPipe, ReasonTextPipe, TimetableGridComponent],
  templateUrl: './student-plan.component.html',
  styleUrls: ['../../../shared/page.scss', './student-plan.component.scss'],
})
export class StudentPlanComponent {
  private readonly planning = inject(PlanningService);
  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);

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
        return {
          code,
          title: courseTitle(course, lang),
          credits: course?.credits ?? 0,
          section: this.sectionById().get(sectionId),
          reasons: (recommended?.reasons ?? assessed?.reasons ?? []).filter((r) => STUDENT_PLAN_REASONS.includes(r.code)),
          critical: this.critical().has(code),
          rank: assessed?.priorityRank ?? null,
          chain: assessed?.chainLength ?? 0,
          unlocks: assessed?.unlocks ?? 0,
          alternatives: recommended?.alternatives.map((a) => ({ sectionCode: a.sectionCode, reasons: a.reasons })) ?? [],
        };
      })
      .sort((a, b) => (a.rank ?? 999) - (b.rank ?? 999) || a.code.localeCompare(b.code));
  });

  protected readonly totalCredits = computed(() => this.rows().reduce((sum, r) => sum + r.credits, 0));

  /** Classes on the timetable grid; online classes (no meetings) are listed under it. */
  protected readonly blocks = computed<TimetableBlock[]>(() =>
    this.rows().flatMap((r) => (r.section?.meetings ?? []).map((m, i) => ({ key: `${r.code}-${i}`, code: r.code, label: r.title, day: m.day, start: m.start, end: m.end }))),
  );
  protected readonly online = computed(() => this.rows().filter((r) => r.section && r.section.meetings.length === 0).map((r) => ({ code: r.code, label: r.title })));
  protected readonly gridDays = computed(() => (this.blocks().some((b) => b.day === 'Saturday') ? [...TEACHING_DAYS, 'Saturday' as const] : TEACHING_DAYS));

  /** Courses the student could add (eligible now or with a condition), grouped by requirement group. */
  protected readonly candidateGroups = computed(() => {
    if (!this.view.hasValue()) return [];
    const v = this.view.value();
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

  protected toggleCourse(code: string, checked: boolean): void {
    this.selection.update((sel) => {
      const next = { ...sel };
      if (!checked) delete next[code];
      else {
        const recommended = this.view.value()?.recommendation.plan.find((p) => p.courseCode === code)?.sectionId;
        const options = this.sectionsFor(code).filter((s) => s.seatsLeft > 0);
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
}
