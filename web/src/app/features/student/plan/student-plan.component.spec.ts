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
      { courseCode: 'ITC201', credits: 3, sectionId: 's1', sectionCode: '01', priorityRank: 1, reasons: [{ code: 'UnlocksOtherCourses', courses: [], value: '5' }], alternatives: [] },
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

const fake: Partial<PlanningService> = {
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

describe('planner view (brief §9)', () => {
  it('shows the recommended plan first, with one primary "Send to my advisor"', async () => {
    const el = await renderPlanner();
    expect(el.querySelector('h2')!.textContent).toContain('Here is the plan we recommend for Spring 2027');
    const primary = [...el.querySelectorAll('button[mat-flat-button]')].map((b) => b.textContent!.trim());
    expect(primary).toHaveLength(1);
    expect(primary[0]).toContain('Send to my advisor');
    expect(el.textContent).toContain('Change courses');
  });

  it('marks critical-path courses in gold and explains why in plain words', async () => {
    const el = await renderPlanner();
    expect(el.querySelector('.course .liwa-critical')!.textContent).toContain('Take on time');
    expect(el.textContent).toContain('Take this soon: 5 other courses depend on it');
  });

  it('shows actionable checks and a separate "not eligible" panel grouped by reason', async () => {
    const el = await renderPlanner();
    expect(el.textContent).toContain('Add 9 more credits to reach the 12-credit minimum');
    const blocked = el.querySelector('details.blocked')!;
    expect(blocked.querySelector('summary')!.textContent).toContain('Not eligible this semester (2)');
    expect(blocked.textContent).toContain('Need to pass another course first (1)');
    expect(blocked.textContent).toContain('Not offered this semester (1)');
  });

  it('shows "Not available" when no AI assistant is configured', async () => {
    const el = await renderPlanner();
    expect(el.querySelector('app-explainer-panel')!.textContent).toContain('Not available');
  });

  it('renders in Arabic with Arabic titles and isolated course codes', async () => {
    const el = await renderPlanner('ar');
    expect(el.textContent).toContain('البرمجة 2');
    const code = el.querySelector('.course .code')!;
    expect(code.tagName).toBe('BDI');
    expect(code.textContent).toBe('ITC201');
  });
});
