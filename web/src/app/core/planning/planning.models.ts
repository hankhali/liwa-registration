import { localDigits } from '../i18n/digits';

export type Day ='Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';
export const TEACHING_DAYS: Day[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

export interface TimeWindow {
  day: Day;
  start: string; // "17:00"
  end: string;
}

export interface Meeting {
  day: Day;
  start: string; // "17:00:00"
  end: string;
}

export interface Reason {
  code: string;
  courses: string[];
  value: string | null;
}

export interface CreditLimits {
  minCredits: number;
  maxCredits: number;
  ruleName: string;
}

export interface PlannedCourse {
  courseCode: string;
  credits: number;
  sectionId: string;
  sectionCode: string;
  priorityRank: number;
  reasons: Reason[];
  alternatives: { sectionId: string; sectionCode: string; reasons: Reason[] }[];
}

export type CourseStatus = 'Planned' | 'Eligible' | 'EligibleWithCondition' | 'NotEligible';

export interface CourseAssessment {
  courseCode: string;
  credits: number;
  groupKey: string;
  status: CourseStatus;
  priorityRank: number;
  unlocks: number;
  chainLength: number;
  reasons: Reason[];
}

export interface ProjectedTerm {
  termCode: string;
  courses: string[];
  credits: number;
  electiveCredits: number;
}

export interface TermProjection {
  graduationTerm: string | null;
  termsRemaining: number;
  terms?: ProjectedTerm[];
}

export interface GraduationProjection {
  withSummer: TermProjection;
  withoutSummer: TermProjection;
}

export interface RequirementGroupInfo {
  key: string;
  name: string;
  kind: 'AllOf' | 'ChooseCredits' | 'AnyCourse';
}

/** Saved free time and planner settings (null preset = never chosen: show the preset cards). */
export interface Preferences {
  preset: AvailabilityPreset | null;
  availability: TimeWindow[];
  delivery: DeliveryPreference;
  maxCredits: number | null;
  draftSectionIds: string[];
}

export type AvailabilityPreset = 'daytime' | 'evenings' | 'weekends' | 'custom' | 'any';

export interface PlannerResult {
  termCode: string;
  limits: CreditLimits | null;
  maxCredits: number;
  plan: PlannedCourse[];
  totalCredits: number;
  courses: CourseAssessment[];
  warnings: Reason[];
  projection: GraduationProjection;
}

export interface CourseInfo {
  code: string;
  title: string;
  titleAr: string | null;
  credits: number;
}

export interface SectionInfo {
  id: string;
  courseCode: string;
  sectionCode: string;
  delivery: 'OnCampus' | 'Online' | 'Hybrid';
  instructor: string | null;
  room: string | null;
  meetings: Meeting[];
  seatsLeft: number;
}

export type PlanStatus = 'Draft' | 'Submitted' | 'AdvisorReviewing' | 'Approved' | 'Rejected' | 'ChangesProposed' | 'NeedsReview';

export interface SavedPlan {
  id: string;
  status: PlanStatus;
  version: number;
  sectionIds: string[];
  updatedAt: string;
  availability: TimeWindow[] | null;
}

export interface PlanningView {
  termCode: string;
  termName: string;
  advisorName: string | null;
  recommendation: PlannerResult;
  courses: CourseInfo[];
  sections: SectionInfo[];
  saved: SavedPlan | null;
  history: PlanEvent[];
  groups?: RequirementGroupInfo[] | null;
  /** Graduation projection for the saved plan (to compare with the recommendation). */
  savedProjection?: GraduationProjection | null;
  hold?: string | null;
}

export interface PlanEvent {
  at: string;
  actorName: string;
  actorRole: string;
  action: string;
  status: PlanStatus;
  version: number | null;
  note: string | null;
  added: string[];
  removed: string[];
}

export interface PlanCheck {
  totalCredits: number;
  limits: CreditLimits | null;
  maxCredits: number;
  errors: Reason[];
  warnings: Reason[];
}

export type DeliveryPreference = 'Any' | 'OnCampusOnly' | 'OnlineOnly';

/** Statuses where the plan is with the advisor and can't be changed by the student. */
export const SENT_STATUSES: PlanStatus[] = ['Submitted', 'AdvisorReviewing', 'Approved'];

// ---------------------------------------------------------------- free-time presets (brief §9: four cards)

function everyDay(days: Day[], start: string, end: string): TimeWindow[] {
  return days.map((day) => ({ day, start, end: day === 'Friday' && end > '12:00' ? '12:00' : end }));
}

/** The four first-run cards. "custom" starts empty; "any" (skip) means "show every class". */
export const PRESET_CARDS: AvailabilityPreset[] = ['daytime', 'evenings', 'weekends', 'custom'];

export const PRESETS: Record<Exclude<AvailabilityPreset, 'custom'>, TimeWindow[]> = {
  any: [],
  // Full-time daytime: Mon–Fri 8 am–5 pm (Friday until noon).
  daytime: everyDay(TEACHING_DAYS, '08:00', '17:00'),
  // Evenings only: Mon–Thu 5–8 pm (the brief's working-student blocks).
  evenings: everyDay(['Monday', 'Tuesday', 'Wednesday', 'Thursday'], '17:00', '20:00'),
  // Weekends + evenings: Mon–Thu evenings, Friday morning and Saturday daytime (Saturday classes only run
  // when a term teaches on Saturday; see docs/assumptions.md S6).
  weekends: [
    ...everyDay(['Monday', 'Tuesday', 'Wednesday', 'Thursday'], '17:00', '20:00'),
    { day: 'Friday', start: '08:00', end: '12:00' },
    { day: 'Saturday', start: '08:00', end: '17:00' },
  ],
};

// ---------------------------------------------------------------- formatting

/** "17:00:00" → "5:00 pm" (12-hour, brief §4); Arabic uses ص/م. */
export function formatTime(t: string, lang: string): string {
  const [h, m] = t.split(':').map(Number);
  const suffix = lang === 'ar' ? (h < 12 ? 'ص' : 'م') : h < 12 ? 'am' : 'pm';
  const hour = h % 12 === 0 ? 12 : h % 12;
  return localDigits(`${hour}:${String(m).padStart(2, '0')} ${suffix}`, lang);
}

export function timeRange(m: Meeting, lang: string): string {
  return `${formatTime(m.start, lang)}–${formatTime(m.end, lang)}`;
}

/** Groups a section's meetings by identical time: [{days:[Mon,Wed], start, end}]. */
export function meetingPatterns(meetings: Meeting[]): { days: Day[]; start: string; end: string }[] {
  const out: { days: Day[]; start: string; end: string }[] = [];
  for (const m of meetings) {
    const same = out.find((p) => p.start === m.start && p.end === m.end);
    if (same) same.days.push(m.day);
    else out.push({ days: [m.day], start: m.start, end: m.end });
  }
  return out;
}

export function courseTitle(c: CourseInfo | undefined, lang: string): string {
  if (!c) return '';
  return lang === 'ar' && c.titleAr ? c.titleAr : c.title;
}

/** Term code "2027FA" → { season: "Fall", year: 2027 }. */
export function parseTermCode(code: string): { season: 'Spring' | 'Summer' | 'Fall'; year: number } | null {
  const m = /^(\d{4})(SP|SU|FA)$/.exec(code ?? '');
  if (!m) return null;
  return { year: Number(m[1]), season: m[2] === 'SP' ? 'Spring' : m[2] === 'SU' ? 'Summer' : 'Fall' };
}

/** Term code → an ordinal for comparing (Spring < Summer < Fall within a year). */
export function termOrdinal(code: string | null): number | null {
  const t = code ? parseTermCode(code) : null;
  return t ? t.year * 3 + (t.season === 'Spring' ? 0 : t.season === 'Summer' ? 1 : 2) : null;
}

/** Semesters between two graduation terms ("this choice delays graduation by one semester"); summer counts only if included. */
export function semesterDelay(recommended: string | null, selection: string | null, includeSummer: boolean): number {
  const a = termOrdinal(recommended);
  const b = termOrdinal(selection);
  if (a === null || b === null || b <= a) return 0;
  let n = 0;
  for (let o = a + 1; o <= b; o++) if (includeSummer || o % 3 !== 1) n++;
  return n;
}

/** Same key the server uses to match an "I understand" (Planner.WarningKey). */
export function warningKey(r: Reason): string {
  return `${r.code}:${r.courses.join(',')}:${r.value ?? ''}`;
}

/** Groups for the "not available this term" panel. */
export type BlockedGroup = 'prerequisite' | 'notOffered' | 'moreCredits' | 'permission' | 'other';

export function blockedGroup(reasons: Reason[]): BlockedGroup {
  const codes = reasons.map((r) => r.code);
  if (codes.some((c) => ['PrerequisiteNotMet', 'PrerequisiteFailed', 'MinimumGradeNotMet', 'GradeMustBePosted'].includes(c))) return 'prerequisite';
  if (codes.some((c) => ['NotOfferedThisTerm', 'NoSectionsThisTerm'].includes(c))) return 'notOffered';
  if (codes.some((c) => ['StandingRequired', 'CreditsRequired'].includes(c))) return 'moreCredits';
  if (codes.includes('PermissionRequired')) return 'permission';
  return 'other';
}

/**
 * Critical path ("gold" courses): on or next to the longest prerequisite chain left in the student's own plan,
 * so delaying them delays graduation (brief §8: depth over the student's own remaining courses).
 */
export function criticalCodes(courses: CourseAssessment[]): Set<string> {
  const max = Math.max(0, ...courses.map((c) => c.chainLength));
  if (max < 2) return new Set();
  return new Set(courses.filter((c) => c.chainLength >= Math.max(2, max - 1)).map((c) => c.courseCode));
}

/** Reasons a student sees on a planned course (ranking details are for advisors only). */
export const STUDENT_PLAN_REASONS = ['RequiredForDegree', 'CountsTowardElectives', 'UnlocksOtherCourses', 'OnlyOfferedIn', 'WaitingForGrade', 'TakeWithCorequisite'];
