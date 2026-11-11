import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { isolate } from '../../shared/bidi';
import { LanguageService } from '../i18n/language.service';
import { CourseTitles } from './course-titles';
import { AuthService } from '../auth/auth.service';
import { CREDITS_PER_CLASS, Reason, classesFromCredits } from './planning.models';

/** Reasons whose course list means "any one of these" rather than "all of these". */
const OR_LISTS = new Set(['PrerequisiteNotMet']);

/** Section-level reasons that may arrive without a course (the course is the row they sit on). */
const SECTION_ONLY = new Set(['SectionFull', 'OutsideAvailability']);

export interface ReasonContext {
  /** Credits currently selected (for "add N more credits"). */
  total?: number;
  /** Student wording: credit figures as classes first ("Add 2 more classes … (4 classes / 12 credits)"). */
  student?: boolean;
}

/** Reasons with a credit figure; students get a "classes first" version (planner.studentReasons / studentActions). */
const CREDIT_REASONS = new Set(['CreditLimitReached', 'CreditsRequired', 'BelowMinimumCredits', 'OverCreditLimit', 'OnlyCreditsFit']);

/**
 * Turns a planner reason code into a plain sentence in the reader's language (brief §8/§9: the server sends
 * codes + parameters, never prose). Course codes are wrapped in Unicode isolates so Arabic text never reorders
 * them (brief §9: isolate only the code). `mode: 'action'` gives the actionable version used in the Checks box,
 * e.g. BelowMinimumCredits → "Add 9 more credits to reach the 12-credit minimum, or ask your advisor…".
 */
@Pipe({ name: 'reasonText', pure: false })
export class ReasonTextPipe implements PipeTransform {
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly titles = inject(CourseTitles);
  private readonly auth = inject(AuthService);

  transform(r: Reason, mode: 'plain' | 'action' = 'plain', ctx: ReasonContext = {}): string {
    this.language.lang(); // re-render on language change
    // Students count classes; advisors and admins keep credits (display only).
    const context = { ...ctx, student: ctx.student ?? this.auth.role() === 'Student' };
    return reasonSentence(r, (key, params) => this.transloco.translate(key, params), mode, context, (key) => this.has(key), (code) => this.titles.title(code));
  }

  private has(key: string): boolean {
    const t = this.transloco.translate(key);
    return !!t && t !== key;
  }
}

type Translate = (key: string, params?: Record<string, unknown>) => string;

/** Pure version of the pipe (used by tests and by components that need a string). */
export function reasonSentence(
  r: Reason,
  t: Translate,
  mode: 'plain' | 'action',
  ctx: ReasonContext,
  has: (key: string) => boolean,
  title: (code: string) => string = () => '',
): string {
  // Code AND title, always (only the code is isolated, so Arabic never reorders it).
  const codes = r.courses.map((c) => {
    const name = title(c);
    return name ? `${isolate(c)} ${name}` : isolate(c);
  });
  const joiner = t(OR_LISTS.has(r.code) ? 'planner.join.or' : 'planner.join.and');
  const courses = codes.length <= 1 ? (codes[0] ?? '') : `${codes.slice(0, -1).join(', ')} ${joiner} ${codes.at(-1)}`;
  let value = r.value ?? '';
  if (r.code === 'OnlyOfferedIn' || r.code === 'NotOfferedThisTerm' || r.code === 'FallOnlyMissed')
    value = value
      .split(';')
      .filter(Boolean)
      .map((s) => t(`planner.season.${s}`))
      .join(` ${t('planner.join.and')} `);
  if (r.code === 'StandingRequired') value = t(`student.home.studies.standing.${value}`);
  if (r.code === 'AccountHold') value = has(`planner.holds.${value}`) ? t(`planner.holds.${value}`) : value;

  let key = `planner.reasons.${r.code}`;
  if (r.code === 'PrerequisiteFailed' && !r.value) key = 'planner.reasons.PrerequisiteFailedAny';
  else if (r.code === 'UnlocksOtherCourses' && r.value === '1') key = 'planner.reasons.UnlocksOtherCoursesOne';
  else if (SECTION_ONLY.has(r.code) && r.courses.length === 0) key = `planner.reasons.${r.code}Section`;
  else if (r.code === 'ClashesWith' && r.courses.length === 1) key = 'planner.reasons.ClashesWithOne';

  const params: Record<string, unknown> = { courses, value, first: codes[0] ?? '', second: codes[1] ?? '' };
  const n = Number(r.value);
  const total = ctx.total ?? 0;
  const student = !!ctx.student && CREDIT_REASONS.has(r.code);
  if (Number.isFinite(n)) params['classes'] = classesFromCredits(n);
  if (mode === 'action' && has(`planner.actions.${r.code}`)) {
    params['missing'] = Number.isFinite(n) ? Math.max(0, n - total) : '';
    params['over'] = Number.isFinite(n) ? Math.max(0, total - n) : '';
    params['missingClasses'] = Number.isFinite(n) ? Math.ceil(Math.max(0, n - total) / CREDITS_PER_CLASS) : '';
    params['overClasses'] = Number.isFinite(n) ? Math.ceil(Math.max(0, total - n) / CREDITS_PER_CLASS) : '';
    key = student && has(`planner.studentActions.${r.code}`) ? `planner.studentActions.${r.code}` : `planner.actions.${r.code}`;
  } else if (student && has(`planner.studentReasons.${r.code}`)) {
    key = `planner.studentReasons.${r.code}`;
  }
  return t(key, params);
}
