import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../i18n/language.service';
import { Reason } from './planning.models';

/** Reasons whose course list means "any one of these" rather than "all of these". */
const OR_LISTS = new Set(['PrerequisiteNotMet']);

/**
 * Turns a planner reason code into a plain sentence in the reader's language, e.g.
 * PrerequisiteNotMet [ITC304, ITC301] → "You need to pass ITC304 or ITC301 first".
 */
@Pipe({ name: 'reasonText', pure: false })
export class ReasonTextPipe implements PipeTransform {
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  transform(r: Reason): string {
    this.language.lang(); // re-render on language change
    const t = (key: string, params?: Record<string, unknown>) => this.transloco.translate(key, params);
    const joiner = t(OR_LISTS.has(r.code) ? 'planner.join.or' : 'planner.join.and');
    const courses = r.courses.length <= 1 ? (r.courses[0] ?? '') : `${r.courses.slice(0, -1).join(', ')} ${joiner} ${r.courses.at(-1)}`;
    let value = r.value ?? '';
    if (r.code === 'OnlyOfferedIn' || r.code === 'NotOfferedThisTerm')
      value = value
        .split(';')
        .filter(Boolean)
        .map((s) => t(`planner.season.${s}`))
        .join(` ${t('planner.join.and')} `);
    if (r.code === 'StandingRequired') value = t(`student.home.studies.standing.${value}`);
    const key =
      r.code === 'PrerequisiteFailed' && !r.value
        ? 'planner.reasons.PrerequisiteFailedAny'
        : r.code === 'UnlocksOtherCourses' && r.value === '1'
          ? 'planner.reasons.UnlocksOtherCoursesOne'
          : `planner.reasons.${r.code}`;
    return t(key, { courses, value, first: r.courses[0] ?? '', second: r.courses[1] ?? '' });
  }
}
