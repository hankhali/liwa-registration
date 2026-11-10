import { describe, expect, it } from 'vitest';
import {
  CourseAssessment,
  PRESETS,
  PRESET_CARDS,
  blockedGroup,
  criticalCodes,
  formatTime,
  meetingPatterns,
  parseTermCode,
  semesterDelay,
  termOrdinal,
  warningKey,
} from './planning.models';

describe('planning helpers', () => {
  it('shows times in 12-hour format in both languages', () => {
    expect(formatTime('17:00:00', 'en')).toBe('5:00 pm');
    expect(formatTime('09:05:00', 'en')).toBe('9:05 am');
    expect(formatTime('12:30:00', 'en')).toBe('12:30 pm');
    expect(formatTime('17:00:00', 'ar')).toBe('5:00 م');
  });

  it('offers the four first-run preset cards from the brief', () => {
    expect(PRESET_CARDS).toEqual(['daytime', 'evenings', 'weekends', 'custom']);
  });

  it("never offers Friday afternoon (Jumu'ah) in any preset", () => {
    for (const windows of Object.values(PRESETS))
      for (const w of windows.filter((x) => x.day === 'Friday')) expect(w.end <= '12:00').toBe(true);
    expect(PRESETS.daytime.find((w) => w.day === 'Friday')!.end).toBe('12:00');
  });

  it('the evenings preset is Monday to Thursday, 5 to 8 pm', () => {
    expect(PRESETS.evenings.map((w) => w.day)).toEqual(['Monday', 'Tuesday', 'Wednesday', 'Thursday']);
    expect(PRESETS.evenings.every((w) => w.start === '17:00' && w.end === '20:00')).toBe(true);
  });

  it('weekends + evenings adds Saturday and never Sunday', () => {
    expect(PRESETS.weekends.some((w) => w.day === 'Saturday')).toBe(true);
    expect(Object.values(PRESETS).flat().some((w) => w.day === 'Sunday')).toBe(false);
  });

  it('groups meeting days that share a time', () => {
    const p = meetingPatterns([
      { day: 'Monday', start: '17:00:00', end: '18:15:00' },
      { day: 'Wednesday', start: '17:00:00', end: '18:15:00' },
    ]);
    expect(p).toEqual([{ days: ['Monday', 'Wednesday'], start: '17:00:00', end: '18:15:00' }]);
  });

  it('builds the same warning key as the server', () => {
    expect(warningKey({ code: 'WaitingForGrade', courses: ['ITC301', 'ITC201'], value: null })).toBe('WaitingForGrade:ITC301,ITC201:');
    expect(warningKey({ code: 'BelowMinimumCredits', courses: [], value: '12' })).toBe('BelowMinimumCredits::12');
  });

  it('groups unavailable courses by the main reason', () => {
    expect(blockedGroup([{ code: 'PrerequisiteFailed', courses: ['MCM201'], value: 'C' }])).toBe('prerequisite');
    expect(blockedGroup([{ code: 'NotOfferedThisTerm', courses: [], value: 'Fall' }])).toBe('notOffered');
    expect(blockedGroup([{ code: 'StandingRequired', courses: [], value: 'Junior' }])).toBe('moreCredits');
    expect(blockedGroup([{ code: 'PermissionRequired', courses: [], value: null }])).toBe('permission');
  });

  it('reads and orders term codes (Spring < Summer < Fall)', () => {
    expect(parseTermCode('2027FA')).toEqual({ season: 'Fall', year: 2027 });
    expect(parseTermCode('bad')).toBeNull();
    expect(termOrdinal('2027SP')! < termOrdinal('2027SU')!).toBe(true);
    expect(termOrdinal('2027FA')! < termOrdinal('2028SP')!).toBe(true);
  });

  it('counts the semesters a selection delays graduation', () => {
    expect(semesterDelay('2029SP', '2029SP', false)).toBe(0);
    expect(semesterDelay('2029SP', '2029FA', false)).toBe(1); // summer skipped
    expect(semesterDelay('2029SP', '2029FA', true)).toBe(2);
    expect(semesterDelay('2029FA', '2029SP', false)).toBe(0); // earlier is not a delay
    expect(semesterDelay(null, '2029SP', false)).toBe(0);
  });

  it('marks the longest prerequisite chains as the critical (gold) path', () => {
    const c = (code: string, chainLength: number): CourseAssessment =>
      ({ courseCode: code, credits: 3, groupKey: 'CORE', status: 'Eligible', priorityRank: 1, unlocks: 0, chainLength, reasons: [] }) as CourseAssessment;
    expect([...criticalCodes([c('A', 5), c('B', 4), c('C', 2), c('D', 1)])].sort()).toEqual(['A', 'B']);
    expect(criticalCodes([c('A', 1), c('B', 1)]).size).toBe(0);
  });
});
