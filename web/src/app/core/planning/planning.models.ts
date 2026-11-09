export type Day = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';
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

export interface TermProjection {
  graduationTerm: string | null;
  termsRemaining: number;
}

export interface PlannerResult {
  termCode: string;
  limits: CreditLimits | null;
  maxCredits: number;
  plan: PlannedCourse[];
  totalCredits: number;
  courses: CourseAssessment[];
  warnings: Reason[];
  projection: { withSummer: TermProjection; withoutSummer: TermProjection };
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
  history: {
    at: string;
    actorName: string;
    actorRole: string;
    action: string;
    status: PlanStatus;
    version: number | null;
    note: string | null;
    added: string[];
    removed: string[];
  }[];
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

// ---------------------------------------------------------------- free-time presets

export type PresetKey = 'any' | 'evenings' | 'mornings' | 'daytime' | 'custom';

export type Period = 'morning' | 'afternoon' | 'evening';
export const PERIODS: { key: Period; start: string; end: string }[] = [
  { key: 'morning', start: '08:00', end: '12:00' },
  { key: 'afternoon', start: '12:00', end: '17:00' },
  { key: 'evening', start: '17:00', end: '20:00' },
];

function everyDay(days: Day[], start: string, end: string): TimeWindow[] {
  return days.map((day) => ({ day, start, end: day === 'Friday' && end > '12:00' ? '12:00' : end }));
}

export const PRESETS: Record<Exclude<PresetKey, 'custom'>, TimeWindow[]> = {
  any: [],
  evenings: everyDay(['Monday', 'Tuesday', 'Wednesday', 'Thursday'], '17:00', '20:00'),
  mornings: everyDay(TEACHING_DAYS, '08:00', '12:00'),
  daytime: everyDay(TEACHING_DAYS, '08:00', '17:00'),
};

/** Custom grid → windows, merging neighbouring periods so a 11:00–12:15 class fits "morning + afternoon". */
export function windowsFromGrid(grid: Record<string, boolean>): TimeWindow[] {
  const windows: TimeWindow[] = [];
  for (const day of TEACHING_DAYS) {
    let current: TimeWindow | null = null;
    for (const p of PERIODS) {
      const allowed = !(day === 'Friday' && p.key !== 'morning');
      if (allowed && grid[`${day}.${p.key}`]) {
        if (current && current.end === p.start) current.end = p.end;
        else {
          current = { day, start: p.start, end: p.end };
          windows.push(current);
        }
      } else current = null;
    }
  }
  return windows;
}

// ---------------------------------------------------------------- formatting

/** "17:00:00" → "5:00 pm" (12-hour, brief §4); Arabic uses ص/م. */
export function formatTime(t: string, lang: string): string {
  const [h, m] = t.split(':').map(Number);
  const suffix = lang === 'ar' ? (h < 12 ? 'ص' : 'م') : h < 12 ? 'am' : 'pm';
  const hour = h % 12 === 0 ? 12 : h % 12;
  return `${hour}:${String(m).padStart(2, '0')} ${suffix}`;
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

/** Same key the server uses to match an "I understand" (Planner.WarningKey). */
export function warningKey(r: Reason): string {
  return `${r.code}:${r.courses.join(',')}:${r.value ?? ''}`;
}

/** Groups for the "not available this term" panel. */
export type BlockedGroup = 'prerequisite' | 'notOffered' | 'moreCredits' | 'permission' | 'other';

export function blockedGroup(reasons: Reason[]): BlockedGroup {
  const codes = reasons.map((r) => r.code);
  if (codes.some((c) => ['PrerequisiteNotMet', 'PrerequisiteFailed', 'MinimumGradeNotMet'].includes(c))) return 'prerequisite';
  if (codes.some((c) => ['NotOfferedThisTerm', 'NoSectionsThisTerm'].includes(c))) return 'notOffered';
  if (codes.some((c) => ['StandingRequired', 'CreditsRequired'].includes(c))) return 'moreCredits';
  if (codes.includes('PermissionRequired')) return 'permission';
  return 'other';
}

/** Reasons a student sees on a planned course (ranking details are for advisors only). */
export const STUDENT_PLAN_REASONS = ['RequiredForDegree', 'CountsTowardElectives', 'UnlocksOtherCourses', 'OnlyOfferedIn', 'WaitingForGrade', 'TakeWithCorequisite'];
