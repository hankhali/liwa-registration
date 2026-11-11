import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { PlanningView } from '../core/planning/planning.models';
import { PlanEditorComponent } from './plan-editor.component';
import { setupI18n } from './testing';

const view: PlanningView = {
  termCode: '2027SP',
  termName: 'Spring 2027',
  advisorName: null,
  recommendation: {
    termCode: '2027SP',
    limits: { minCredits: 12, maxCredits: 18, ruleName: 'Standard' },
    maxCredits: 18,
    plan: [{ courseCode: 'ITC201', credits: 3, sectionId: 's1', sectionCode: '01', priorityRank: 1, reasons: [], alternatives: [] }],
    totalCredits: 3,
    courses: [
      { courseCode: 'ITC201', credits: 3, groupKey: 'CORE', status: 'Planned', priorityRank: 1, unlocks: 5, chainLength: 4, reasons: [{ code: 'UnlocksOtherCourses', courses: [], value: '5' }] },
      { courseCode: 'ENG102', credits: 3, groupKey: 'CORE', status: 'Eligible', priorityRank: 2, unlocks: 0, chainLength: 1, reasons: [] },
      { courseCode: 'ITC301', credits: 3, groupKey: 'CORE', status: 'NotEligible', priorityRank: 3, unlocks: 0, chainLength: 1, reasons: [] },
    ],
    warnings: [],
    projection: { withSummer: { graduationTerm: '2030SP', termsRemaining: 7 }, withoutSummer: { graduationTerm: '2030SP', termsRemaining: 7 } },
  },
  courses: [
    { code: 'ITC201', title: 'Programming II', titleAr: null, credits: 3 },
    { code: 'ENG102', title: 'English Composition II', titleAr: null, credits: 3 },
    { code: 'ITC301', title: 'Data Structures', titleAr: null, credits: 3 },
  ],
  sections: [
    { id: 's1', courseCode: 'ITC201', sectionCode: '01', delivery: 'OnCampus', instructor: null, room: null, meetings: [{ day: 'Monday', start: '10:00:00', end: '11:15:00' }], seatsLeft: 10 },
    { id: 's2', courseCode: 'ENG102', sectionCode: '02', delivery: 'OnCampus', instructor: null, room: null, meetings: [{ day: 'Tuesday', start: '10:00:00', end: '10:50:00' }], seatsLeft: 4 },
  ],
  saved: null,
  history: [],
  groups: [{ key: 'CORE', name: 'Major requirements', kind: 'AllOf' }],
};

describe('two-panel planner (shared by student "Change courses" and advisor "Suggest changes")', () => {
  it('offers only classes the student can take, with title and one reason, and emits the new selection', () => {
    setupI18n('en');
    const f = TestBed.createComponent(PlanEditorComponent);
    f.componentRef.setInput('view', view);
    f.componentRef.setInput('selection', { ITC201: 's1' });
    let emitted: Record<string, string> | null = null;
    f.componentInstance.selectionChange.subscribe((s) => (emitted = s));
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Programming II');
    expect(el.textContent).toContain('English Composition II');
    expect(el.textContent).not.toContain('Data Structures'); // can't take it yet
    expect(el.textContent).toContain('Take this soon: 5 other classes depend on it');
    expect(el.querySelector('.counter')!.textContent).toContain('3 credits');

    const eng = [...el.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')][1];
    eng.click();
    f.detectChanges();
    expect(emitted).toEqual({ ITC201: 's1', ENG102: 's2' });
  });
});
