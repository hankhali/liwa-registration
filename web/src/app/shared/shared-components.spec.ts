import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { describe, expect, it } from 'vitest';
import { reasonSentence } from '../core/planning/reason-text.pipe';
import { LRI, PDI, isolate } from './bidi';
import { EmptyStateComponent } from './empty-state.component';
import { PageHeaderComponent } from './page-header.component';
import { PlanHistoryComponent, withVersionSteps } from './plan-history.component';
import { StatCardComponent } from './stat-card.component';
import { StatusChipComponent } from './status-chip.component';
import { setupI18n } from './testing';
import { TimetableGridComponent, hoursFromWindows, overlaps, windowsFromHours } from './timetable-grid.component';

async function render<T>(type: Type<T>, inputs: Record<string, unknown>) {
  const f = TestBed.createComponent(type);
  for (const [k, v] of Object.entries(inputs)) f.componentRef.setInput(k, v);
  f.detectChanges();
  await f.whenStable();
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('clash rule (end-exclusive, brief §8)', () => {
  it('back-to-back classes are not a clash', () => {
    expect(overlaps({ start: '10:00', end: '10:50' }, { start: '10:50', end: '11:40' })).toBe(false);
  });
  it('a one-minute overlap is a clash', () => {
    expect(overlaps({ start: '10:00', end: '10:51' }, { start: '10:50', end: '11:40' })).toBe(true);
  });
});

describe('free-time editor helpers', () => {
  it('turns hour cells into merged windows and back', () => {
    const cells = new Set(['Monday.8', 'Monday.9', 'Monday.11', 'Tuesday.17']);
    const windows = windowsFromHours(cells, ['Monday', 'Tuesday'], 8, 21);
    expect(windows).toEqual([
      { day: 'Monday', start: '08:00', end: '10:00' },
      { day: 'Monday', start: '11:00', end: '12:00' },
      { day: 'Tuesday', start: '17:00', end: '18:00' },
    ]);
    expect([...hoursFromWindows(windows, 8, 21)].sort()).toEqual([...cells].sort());
  });
});

describe('status chip', () => {
  it('shows the status in plain words with a fixed colour class', async () => {
    setupI18n('en');
    const el = await render(StatusChipComponent, { status: 'ChangesProposed' });
    expect(el.textContent).toContain('Changes suggested');
    expect(el.querySelector('.chip')!.className).toContain('status-ChangesProposed');
  });

  it('renders in Arabic', async () => {
    setupI18n('ar');
    const el = await render(StatusChipComponent, { status: 'Approved' });
    expect(el.textContent).toContain('معتمدة');
  });
});

describe('page header, stat card, empty state', () => {
  it('render their text', async () => {
    setupI18n('en');
    expect((await render(PageHeaderComponent, { heading: 'My students', subtitle: 'Everyone' })).querySelector('h1')!.textContent).toBe('My students');
    const stat = await render(StatCardComponent, { label: 'Plans waiting', value: 3, attention: true });
    expect(stat.querySelector('.value')!.textContent).toBe('3');
    expect(stat.querySelector('.attention')).not.toBeNull();
    expect((await render(EmptyStateComponent, { heading: 'Nothing here' })).querySelector('[role=status]')!.textContent).toContain('Nothing here');
  });
});

describe('timetable grid', () => {
  const blocks = [
    { key: 'a', code: 'ITC201', day: 'Monday', start: '10:00', end: '11:15' },
    { key: 'b', code: 'CSC102', day: 'Monday', start: '11:00', end: '12:15' },
    { key: 'c', code: 'MTH101', day: 'Tuesday', start: '10:00', end: '10:50' },
    { key: 'd', code: 'ENG101', day: 'Tuesday', start: '10:50', end: '11:40' },
  ];

  it('marks only overlapping classes as clashing', async () => {
    setupI18n('en');
    const el = await render(TimetableGridComponent, { blocks });
    const clashing = [...el.querySelectorAll('.block.clash .code')].map((e) => e.textContent);
    expect(clashing.sort()).toEqual(['CSC102', 'ITC201']);
    expect(el.textContent).toContain('Two classes are at the same time');
  });

  it("shades Friday afternoon (Jumu'ah) and lists online classes", async () => {
    setupI18n('en');
    const el = await render(TimetableGridComponent, { blocks: [], online: [{ code: 'BUS201' }] });
    expect(el.querySelector('.blocked')!.textContent).toContain('No classes Friday afternoon');
    expect(el.querySelector('.online')!.textContent).toContain('BUS201');
  });

  it('isolates course codes on Arabic lines (bidi), and only the code', async () => {
    setupI18n('ar');
    const el = await render(TimetableGridComponent, { blocks: blocks.slice(2) });
    const codes = [...el.querySelectorAll('.block bdi')];
    expect(codes.length).toBe(2);
    for (const c of codes) {
      expect(c.getAttribute('dir')).toBe('ltr');
      expect(c.textContent).toMatch(/^[A-Z]{3}\d{3}$/); // the code alone, no title inside the isolate
    }
    // The screen-reader list uses Arabic day names around the isolated code.
    expect(el.querySelector('ul.visually-hidden')!.textContent).toContain('الثلاثاء');
  });

  it('works as a free-time editor with toggle buttons and no Friday-afternoon cells', async () => {
    setupI18n('en');
    const f = TestBed.createComponent(TimetableGridComponent);
    f.componentRef.setInput('editable', true);
    f.componentRef.setInput('free', []);
    f.detectChanges();
    const cells = [...(f.nativeElement as HTMLElement).querySelectorAll('button.cell')] as HTMLButtonElement[];
    expect(cells.length).toBe(5 * 13 - 9); // 8 am–9 pm on 5 days, minus Friday from noon
    cells[0].click();
    f.detectChanges();
    expect(f.componentInstance.free()).toEqual([{ day: 'Monday', start: '08:00', end: '09:00' }]);
    expect(cells[0].getAttribute('aria-pressed')).toBe('true');
  });
});

describe('reason sentences', () => {
  it('turn codes into plain sentences with each course code isolated', () => {
    setupI18n('ar');
    const t = TestBed.inject(TranslocoService);
    const tr = (k: string, p?: Record<string, unknown>) => t.translate(k, p);
    const has = (k: string) => t.translate(k) !== k;
    const s = reasonSentence({ code: 'PrerequisiteNotMet', courses: ['ITC304', 'ITC301'], value: null }, tr, 'plain', {}, has);
    expect(s).toContain(isolate('ITC304'));
    expect(s).toContain(isolate('ITC301'));
    // Only the code sits inside the isolate.
    expect(s.split(LRI).slice(1).map((x) => x.split(PDI)[0])).toEqual(['ITC304', 'ITC301']);
  });

  it('give actionable checks in English', () => {
    setupI18n('en');
    const t = TestBed.inject(TranslocoService);
    const tr = (k: string, p?: Record<string, unknown>) => t.translate(k, p);
    const has = (k: string) => t.translate(k) !== k;
    expect(reasonSentence({ code: 'BelowMinimumCredits', courses: [], value: '12' }, tr, 'action', { total: 3 }, has)).toBe(
      'Add 9 more credits to reach the 12-credit minimum, or ask your advisor to approve a lighter load.',
    );
    expect(reasonSentence({ code: 'SectionFull', courses: [], value: null }, tr, 'plain', {}, has)).toBe('All seats are taken');
  });
});

describe('plan history', () => {
  const ev = (action: string, version: number | null, added: string[] = []) => ({
    at: '2026-11-09T10:00:00Z', actorName: 'X', actorRole: 'Advisor', action, status: 'Submitted' as const, version, note: null, added, removed: [],
  });

  it('shows each change as "Version 1 → 2"', async () => {
    const events = [ev('submitted', 1, ['ITC201 (01)']), ev('reviewing', 1), ev('proposed', 2, ['CSC102 (02)'])];
    expect(withVersionSteps(events).map((e) => e.fromVersion)).toEqual([null, null, 1]);
    setupI18n('en');
    const el = await render(PlanHistoryComponent, { events });
    expect(el.textContent).toContain('Version 1 → 2');
    expect(el.querySelector('.added bdi')!.getAttribute('dir')).toBe('ltr');
  });
});
