import { HttpTestingController } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { NAV } from '../../core/layout/nav';
import { PlanningView, Preferences } from '../../core/planning/planning.models';
import { PlanningService } from '../../core/planning/planning.service';
import { setupI18n } from '../../shared/testing';
import { GraduationPage } from './graduation.page';
import { MyPlanPage } from './my-plan.page';

const prefs: Preferences = { preset: 'any', availability: [], delivery: 'Any', maxCredits: null, draftSectionIds: [] };
const meeting = (day: 'Monday' | 'Tuesday', start: string, end: string) => [{ day, start, end }];

const base: PlanningView = {
  termCode: '2027SP',
  termName: 'Spring 2027',
  advisorName: 'Dr. Fatima',
  recommendation: {
    termCode: '2027SP',
    limits: { minCredits: 12, maxCredits: 18, ruleName: 'Standard' },
    maxCredits: 18,
    plan: [],
    totalCredits: 0,
    courses: [],
    warnings: [],
    projection: { withSummer: { graduationTerm: '2029SP', termsRemaining: 6 }, withoutSummer: { graduationTerm: '2029SP', termsRemaining: 6 } },
  },
  courses: [
    { code: 'BUS220', title: 'Business Statistics', titleAr: null, credits: 3 },
    { code: 'FIN201', title: 'Principles of Finance', titleAr: null, credits: 3 },
    { code: 'BUS410', title: 'Business Internship', titleAr: null, credits: 3 },
  ],
  sections: [
    { id: 'a', courseCode: 'BUS220', sectionCode: 'E1', delivery: 'OnCampus', instructor: null, room: null, meetings: meeting('Monday', '17:00:00', '18:15:00'), seatsLeft: 5 },
    { id: 'b', courseCode: 'FIN201', sectionCode: '01', delivery: 'OnCampus', instructor: null, room: null, meetings: meeting('Tuesday', '11:00:00', '12:15:00'), seatsLeft: 5 },
  ],
  saved: null,
  history: [],
};

async function render<T>(component: Type<T>, view: PlanningView, lang: 'en' | 'ar' = 'en'): Promise<HTMLElement> {
  const fake: Partial<PlanningService> = {
    preferences: () => Promise.resolve(prefs),
    view: () => Promise.resolve(view),
    currentView: () => Promise.resolve({ prefs, view }),
    projection: () => Promise.resolve(view.recommendation.projection),
  };
  setupI18n(lang, [{ provide: PlanningService, useValue: fake }]);
  const f = TestBed.createComponent(component);
  const http = TestBed.inject(HttpTestingController);
  for (let i = 0; i < 8; i++) {
    f.detectChanges();
    await new Promise((r) => setTimeout(r, 0));
    http.match(() => true);
  }
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('student menu (follow-up 1)', () => {
  it('uses labels that match the page headings: Home, My free time, My plan, Graduation, Messages', () => {
    const labels = NAV.Student.map((i) => i.label ?? i.key);
    expect(labels).toEqual(['home', 'studentFreeTime', 'studentMyPlan', 'graduationShort', 'messages']);
    // Staff menus keep the brief's names.
    expect(NAV.Advisor.every((i) => !i.label)).toBe(true);
  });
});

describe('My Plan: suggested changes side by side (follow-up 2)', () => {
  const view: PlanningView = {
    ...base,
    saved: { id: 'p', status: 'ChangesProposed', version: 2, sectionIds: ['b'], previousSectionIds: ['a', 'b'], updatedAt: '2026-11-10T10:00:00Z', availability: null },
    history: [
      { at: '2026-11-10T09:00:00Z', actorName: 'Maryam', actorRole: 'Student', action: 'submitted', status: 'Submitted', version: 1, note: null, added: ['BUS220 (E1)', 'FIN201 (01)'], removed: [] },
      { at: '2026-11-10T10:00:00Z', actorName: 'Dr. Fatima', actorRole: 'Advisor', action: 'proposed', status: 'ChangesProposed', version: 2, note: 'One class fewer.', added: [], removed: ['BUS220 (E1)'] },
    ],
  };

  it('shows what was sent with removed classes struck through, next to the advisor version, above Accept / Fix my plan', async () => {
    const el = await render(MyPlanPage, view);
    const sides = [...el.querySelectorAll('.compare .side')];
    expect(sides.map((s) => s.querySelector('h3')!.textContent!.trim())).toEqual(['What you sent', "With your advisor's changes"]);
    expect(sides[0].querySelector('li.removed')!.textContent).toContain('Business Statistics');
    expect(sides[0].textContent).toContain('6 credits');
    expect(sides[1].textContent).not.toContain('Business Statistics');
    expect(sides[1].textContent).toContain('3 credits');
    const buttons = [...el.querySelectorAll('.proposal .actions [mat-flat-button], .proposal .actions [mat-stroked-button]')].map((b) => b.textContent!.trim());
    expect(buttons).toEqual(['Accept the changes', 'Fix my plan']);
    // One gold button on the page: Accept (not repeated in the header).
    expect(el.querySelectorAll('[mat-flat-button]')).toHaveLength(1);
  });

  it('highlights added classes on the advisor side', async () => {
    const swapped = { ...view, saved: { ...view.saved!, sectionIds: ['a', 'b'], previousSectionIds: ['b'] } };
    const el = await render(MyPlanPage, swapped);
    expect(el.querySelectorAll('.compare .side')[1].querySelector('li.added')!.textContent).toContain('Business Statistics');
  });
});

describe('Graduation: summer-only class (follow-up 3)', () => {
  it('explains in one line why the summer toggle changes nothing', async () => {
    const view: PlanningView = {
      ...base,
      recommendation: {
        ...base.recommendation,
        projection: {
          withSummer: { graduationTerm: '2029SP', termsRemaining: 6, terms: [] },
          withoutSummer: {
            graduationTerm: '2029SP',
            termsRemaining: 6,
            terms: [
              { termCode: '2027SP', courses: ['FIN201'], credits: 3, electiveCredits: 0 },
              { termCode: '2028SU', courses: ['BUS410'], credits: 3, electiveCredits: 0 },
              { termCode: '2029SP', courses: ['BUS220'], credits: 3, electiveCredits: 0 },
            ],
          },
        },
      },
    };
    const el = await render(GraduationPage, view);
    const note = el.querySelector('.summer-note')!;
    expect(note.textContent).toContain("Summer classes don't change your date");
    expect(note.textContent).toContain('BUS410');
    expect(note.textContent).toContain('Business Internship');
  });

  it('with evenings only, shows both dates: keeping evenings, and a full-time schedule', async () => {
    const view: PlanningView = {
      ...base,
      recommendation: {
        ...base.recommendation,
        projection: { withSummer: { graduationTerm: '2030SP', termsRemaining: 9, terms: [] }, withoutSummer: { graduationTerm: '2030FA', termsRemaining: 8, terms: [] } },
        fullTimeProjection: { withSummer: { graduationTerm: '2028FA', termsRemaining: 6, terms: [] }, withoutSummer: { graduationTerm: '2029SP', termsRemaining: 5, terms: [] } },
        freeTime: { inPersonSlots: 4, onlinePerTerm: 1, onlineSharePercent: 21 },
      },
    };
    prefs.preset = 'evenings';
    const el = await render(GraduationPage, view);
    prefs.preset = 'any';
    expect(el.querySelector('.keep')!.textContent).toContain('If you keep evenings only: December 2030. With a full-time schedule: May 2029.');
    expect(el.querySelector('.assumes')!.textContent).toContain('This assumes about 1 online class a semester, like this semester.');
  });

  it('shows no such line when summer does change the date', async () => {
    const view: PlanningView = {
      ...base,
      recommendation: {
        ...base.recommendation,
        projection: { withSummer: { graduationTerm: '2028FA', termsRemaining: 5, terms: [] }, withoutSummer: { graduationTerm: '2029SP', termsRemaining: 6, terms: [] } },
      },
    };
    const el = await render(GraduationPage, view);
    expect(el.querySelector('.summer-note')).toBeNull();
    expect(el.querySelector('.summer-line')!.textContent).toContain('Taking summer classes brings your graduation forward by 1 semester: May 2029 → December 2028.');
    expect(el.querySelector('#grad-date')!.textContent).toContain('May 2029');
    expect(el.querySelector('.term-small')!.textContent).toContain('Spring 2028-2029');
  });
});
