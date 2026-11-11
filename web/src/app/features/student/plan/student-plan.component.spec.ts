import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { PlanCheck, PlanningView, Preferences } from '../../../core/planning/planning.models';
import { PlanningService } from '../../../core/planning/planning.service';
import { setupI18n } from '../../../shared/testing';
import { StudentPlanComponent } from './student-plan.component';

const view: PlanningView = {
  termCode: '2027SP',
  termName: 'Spring 2027',
  advisorName: 'Dr. Ahmed',
  recommendation: {
    termCode: '2027SP',
    limits: { minCredits: 12, maxCredits: 18, ruleName: 'Standard' },
    maxCredits: 18,
    plan: [
      { courseCode: 'ITC201', credits: 3, sectionId: 's1', sectionCode: '01', priorityRank: 1, reasons: [{ code: 'UnlocksOtherCourses', courses: [], value: '5' }], alternatives: [], takeFirst: true, chain: [['ITC201'], ['ITC301'], ['ITC401', 'ITC402']] },
    ],
    totalCredits: 3,
    courses: [
      { courseCode: 'ITC201', credits: 3, groupKey: 'CORE', status: 'Planned', priorityRank: 1, unlocks: 5, chainLength: 4, reasons: [] },
      { courseCode: 'ITC301', credits: 3, groupKey: 'CORE', status: 'NotEligible', priorityRank: 2, unlocks: 2, chainLength: 3, reasons: [{ code: 'PrerequisiteNotMet', courses: ['ITC201'], value: null }] },
      { courseCode: 'MCM101', credits: 3, groupKey: 'GENED', status: 'NotEligible', priorityRank: 9, unlocks: 0, chainLength: 1, reasons: [{ code: 'NotOfferedThisTerm', courses: [], value: 'Fall' }] },
    ],
    warnings: [],
    projection: { withSummer: { graduationTerm: '2030SP', termsRemaining: 8 }, withoutSummer: { graduationTerm: '2030SP', termsRemaining: 7 } },
  },
  courses: [
    { code: 'ITC201', title: 'Programming II', titleAr: 'البرمجة 2', credits: 3 },
    { code: 'ITC301', title: 'Data Structures', titleAr: null, credits: 3 },
    { code: 'MCM101', title: 'Media Basics', titleAr: null, credits: 3 },
  ],
  sections: [{ id: 's1', courseCode: 'ITC201', sectionCode: '01', delivery: 'OnCampus', instructor: null, room: null, meetings: [{ day: 'Monday', start: '10:00:00', end: '11:15:00' }], seatsLeft: 10 }],
  saved: null,
  history: [],
  groups: [{ key: 'CORE', name: 'Major requirements', kind: 'AllOf' }],
};

const prefs: Preferences = { preset: 'daytime', availability: [], delivery: 'Any', maxCredits: null, draftSectionIds: [] };
const check: PlanCheck = { totalCredits: 3, limits: view.recommendation.limits, maxCredits: 18, errors: [], warnings: [{ code: 'BelowMinimumCredits', courses: [], value: '12' }] };

const saved: unknown[][] = [];
const fake: Partial<PlanningService> = {
  savePreferences: (...args: unknown[]) => {
    saved.push(args);
    return Promise.resolve(null);
  },
  preferences: () => Promise.resolve(prefs),
  view: () => Promise.resolve(view),
  check: () => Promise.resolve(check),
  saveDraft: () => Promise.resolve(null),
};

async function renderPlanner(lang: 'en' | 'ar' = 'en'): Promise<HTMLElement> {
  setupI18n(lang, [{ provide: PlanningService, useValue: fake }]);
  const f = TestBed.createComponent(StudentPlanComponent);
  const http = TestBed.inject(HttpTestingController);
  // No whenStable(): it would wait for the explainer's HTTP call. Flush it instead while the view settles.
  for (let i = 0; i < 12; i++) {
    f.detectChanges();
    await new Promise((r) => setTimeout(r, 0));
    for (const r of http.match('/api/me/explainer')) r.flush({ available: false });
  }
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('planner view (brief §9, UX pass)', () => {
  it('says what to do first and has one gold action, "Send to my advisor", in the page header', async () => {
    const el = await renderPlanner();
    expect(el.querySelector('h1')!.textContent).toContain('My plan');
    expect(el.querySelector('app-page-header')!.textContent).toContain('Here is the plan we recommend for Spring 2026-2027.');
    expect(el.querySelector('app-page-header')!.textContent).toContain('Happy with it? Send it to your advisor.');
    const primary = [...el.querySelectorAll('[mat-flat-button]')].map((b) => b.textContent!.trim());
    expect(primary).toHaveLength(1);
    expect(primary[0]).toContain('Send to my advisor');
    expect(el.querySelector('app-page-header [mat-flat-button]')).not.toBeNull();
    expect(el.textContent).toContain('Change courses');
  });

  it('shows the credits as a big number and one plain reason per class', async () => {
    const el = await renderPlanner();
    // Students count classes: 1 class big, (3 credits) small.
    expect(el.querySelector('.credits-big .num')!.textContent).toBe('1');
    expect(el.querySelector('.credits-big .small-credits')!.textContent).toContain('(3 credits)');
    expect(el.querySelector('.credits-big .range')!.textContent).toContain('You can take up to 6 classes (18 credits).');
    // The top chain-starting course says "Take this first" and spells out its chain.
    expect(el.querySelector('.course .take-first')!.textContent).toContain('Take this first');
    expect(el.querySelector('.course .chain')!.textContent).toContain('ITC201 → ITC301 → ITC401 & ITC402');
    expect(el.querySelectorAll('.course .why-one')).toHaveLength(1);
    expect(el.textContent).toContain('Take this soon: 5 other classes depend on it');
  });

  it('lists the classes you can\'t take yet, visible, each with code, title and a reason naming the course and its title', async () => {
    const el = await renderPlanner();
    expect(el.textContent).toContain('Add 3 more classes to reach the minimum (4 classes / 12 credits)');
    expect(el.querySelector('details')).toBeNull();
    const cant = el.querySelector('.cant-take')!;
    expect(cant.querySelector('h3')!.textContent).toContain("Classes you can't take yet (2)");
    expect(cant.textContent).toContain('You need to pass another class first (1)');
    expect(cant.textContent).toContain('Data Structures');
    expect(cant.textContent).toContain('Programming II first');
    expect(cant.textContent).toContain('Not offered this semester (1)');
  });

  it('hides the assistant when no AI provider is configured (no empty "Not available" box)', async () => {
    const el = await renderPlanner();
    expect(el.querySelector('app-explainer-panel')!.textContent!.trim()).toBe('');
  });

  it('rebuilds the plan with one tap on "Evenings only"', async () => {
    const el = await renderPlanner();
    const chip = [...el.querySelectorAll<HTMLButtonElement>('.free-chip')].find((b) => b.textContent!.includes('Evenings only'))!;
    expect(chip).toBeTruthy();
    saved.length = 0;
    chip.click();
    await new Promise((r) => setTimeout(r, 0));
    expect(saved).toHaveLength(1);
    expect(saved[0][0]).toBe('evenings');
  });

  it('renders in Arabic with Arabic titles and isolated course codes', async () => {
    const el = await renderPlanner('ar');
    expect(el.textContent).toContain('البرمجة 2');
    const code = el.querySelector('.course .code')!;
    expect(code.tagName).toBe('BDI');
    expect(code.textContent).toBe('ITC201');
  });
});