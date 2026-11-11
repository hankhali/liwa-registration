import { Injectable, inject, signal } from '@angular/core';
import { LanguageService } from '../i18n/language.service';
import { CourseInfo } from './planning.models';

/**
 * Course titles the app has seen (filled from every planning view), so any sentence that names a course can say
 * "ITC201 Database Systems" instead of a bare code (UX rule: every course shows code AND title).
 */
@Injectable({ providedIn: 'root' })
export class CourseTitles {
  private readonly language = inject(LanguageService);
  private readonly map = signal<ReadonlyMap<string, CourseInfo>>(new Map());

  register(courses: readonly CourseInfo[]): void {
    if (courses.length === 0) return;
    const next = new Map(this.map());
    for (const c of courses) next.set(c.code, c);
    this.map.set(next);
  }

  /** Title in the reader's language (English when there is no Arabic title), or '' when unknown. */
  title(code: string): string {
    const c = this.map().get(code);
    if (!c) return '';
    return this.language.lang() === 'ar' && c.titleAr ? c.titleAr : c.title;
  }
}

/** "BUS220 (E1)" (a history entry with its class code) → "BUS220". */
export function courseOnly(entry: string): string {
  return entry.replace(/\s*\([^)]*\)\s*$/, '').trim();
}
