export interface TermInfo {
  code: string;
  name: string;
}

export type Standing = 'Freshman' | 'Sophomore' | 'Junior' | 'Senior';

export interface StudentSummary {
  studentId: string;
  programCode: string;
  programName: string;
  programNameAr: string | null;
  catalogYear: number;
  creditsEarned: number;
  creditsRequired: number;
  inProgressCredits: number;
  standing: Standing;
  gpa: number | null;
  onProbation: boolean;
  advisorName: string | null;
  currentTerm: TermInfo | null;
}

export interface AdviseeSummary {
  userId: string;
  studentId: string;
  displayName: string;
  programCode: string;
  programName: string;
  programNameAr: string | null;
  creditsEarned: number;
  creditsRequired: number;
  gpa: number | null;
  onProbation: boolean;
  standing?: Standing | null;
  hold?: string | null;
  planStatus?: import('../planning/planning.models').PlanStatus | null;
  planId?: string | null;
  projectedGraduation?: string | null;
  termsRemaining?: number | null;
  lastActivity?: string | null;
}

export interface DataOverview {
  currentTerm: TermInfo | null;
  terms: number;
  courses: number;
  programs: number;
  degreePlans: number;
  sectionsThisTerm: number;
  students: number;
  advisors: number;
  lastImportAt: string | null;
}

/** Program name in the reader's language, falling back to English. */
export function programName(p: { programName: string; programNameAr: string | null }, lang: string): string {
  return lang === 'ar' && p.programNameAr ? p.programNameAr : p.programName;
}

/** Whole-number percentage, capped at 100. */
export function percentOf(part: number, whole: number): number {
  return whole > 0 ? Math.min(100, Math.round((part / whole) * 100)) : 0;
}

/** Date and time in 12-hour format with lower-case am/pm, e.g. "9 Nov 2026, 5:00 pm" (brief §4), in the reader's language. */
export function formatDateTime(iso: string, lang: string): string {
  const text = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-AE' : 'en-AE', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).format(new Date(iso));
  return text.replace(/\b(AM|PM)\b/g, (m) => m.toLowerCase());
}

/** GPA always shows two decimals (3.50, not 3.5). */
export function formatGpa(gpa: number): string {
  return gpa.toFixed(2);
}
