import { describe, expect, it } from 'vitest';
import { setArabicDigits } from '../i18n/digits';
import {
  classesAndCredits,
  classesFromCredits,
  classesLeft,
  graduationDate,
  graduationMonth,
  semestersAndSummers,
  summerSentence,
  localizeTermNames,
  termLabel,
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

  it('uses Western digits in Arabic unless UI_ARABIC_DIGITS is on', () => {
    expect(formatTime('17:30:00', 'ar')).toBe('5:30 م');
    setArabicDigits(true);
    try {
      expect(formatTime('17:30:00', 'ar')).toBe('٥:٣٠ م');
      expect(formatTime('17:30:00', 'en')).toBe('5:30 pm');
    } finally {
      setArabicDigits(false);
    }
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

describe('Liwa term names (academic year)', () => {
  const t = (k: string) => ({ 'planner.termSeason.Fall': 'Fall', 'planner.termSeason.Spring': 'Spring', 'planner.termSeason.Summer': 'Summer 1', 'planner.summer2': 'Summer 2' })[k] ?? k;

  it('names terms by academic year: Fall starts it, Spring and Summer 1 end it', () => {
    expect(termLabel('2026FA', t, 'en')).toBe('Fall 2026-2027');
    expect(termLabel('2027SP', t, 'en')).toBe('Spring 2026-2027');
    expect(termLabel('2026SU', t, 'en')).toBe('Summer 1 2025-2026');
  });

  it('rewrites term names in server text, both academic-year and calendar style', () => {
    expect(localizeTermNames('Your plan for Spring 2026-2027', t, 'en')).toBe('Your plan for Spring 2026-2027');
    expect(localizeTermNames('Summer 2 2025-2026 opens', t, 'en')).toBe('Summer 2 2025-2026 opens');
    expect(localizeTermNames('You sent your plan for Spring 2027.', t, 'en')).toBe('You sent your plan for Spring 2026-2027.');
  });
});
describe('graduation dates, month first', () => {
  const months: Record<string, string> = { 'months.5': 'May', 'months.7': 'July', 'months.8': 'August', 'months.12': 'December' };
  const strings: Record<string, string> = {
    'graduation.summerForward': 'Taking summer classes brings your graduation forward by {{ n }} semesters: {{ without }} → {{ with }}.',
    'graduation.summerForwardOne': 'Taking summer classes brings your graduation forward by 1 semester: {{ without }} → {{ with }}.',
    'graduation.summerNoChange': "Summer classes wouldn't change your graduation date.",
  };
  const t = (k: string, p?: Record<string, unknown>) =>
    (months[k] ?? strings[k] ?? k).replace(/\{\{ (\w+) \}\}/g, (_, name: string) => String(p?.[name] ?? ''));

  it('maps each term to the month it ends: Fall → December, Spring → May, Summer 1 → July', () => {
    expect(graduationMonth('2029FA')).toEqual({ month: 12, year: 2029 });
    expect(graduationMonth('2030SP')).toEqual({ month: 5, year: 2030 });
    expect(graduationMonth('2029SU')).toEqual({ month: 7, year: 2029 });
    expect(graduationDate('2030SP', t, 'en')).toBe('May 2030');
    expect(graduationDate('2029FA', t, 'en')).toBe('December 2029');
    expect(graduationDate('2029SU', t, 'en')).toBe('July 2029');
  });

  it('says how far summer classes bring graduation forward', () => {
    expect(summerSentence('2029SP', '2030SP', t, 'en')).toBe('Taking summer classes brings your graduation forward by 2 semesters: May 2030 → May 2029.');
    expect(summerSentence('2029FA', '2030SP', t, 'en')).toBe('Taking summer classes brings your graduation forward by 1 semester: May 2030 → December 2029.');
    // Graduating in a summer still counts the regular semesters saved.
    expect(summerSentence('2029SU', '2029FA', t, 'en')).toBe('Taking summer classes brings your graduation forward by 1 semester: December 2029 → July 2029.');
  });

  it('says when summer classes would not change the date', () => {
    expect(summerSentence('2030SP', '2030SP', t, 'en')).toBe("Summer classes wouldn't change your graduation date.");
    expect(summerSentence(null, '2030SP', t, 'en')).toBe("Summer classes wouldn't change your graduation date.");
  });

  it('keeps the academic year left-to-right in Arabic (no "2027-2026")', () => {
    const ar = (k: string) => ({ 'planner.termSeason.Spring': 'الربيع' })[k] ?? k;
    expect(termLabel('2027SP', ar, 'ar')).toBe('الربيع ⁦2026-2027⁩');
  });

  it('counts semesters left (Fall and Spring) separately from summers', () => {
    expect(semestersAndSummers([{ termCode: '2027SP' }, { termCode: '2027SU' }, { termCode: '2027FA' }, { termCode: '2028SP' }, { termCode: '2028SU' }]))
      .toEqual({ semesters: 3, summers: 2 });
  });
});
describe('classes first, credits second (students)', () => {
  const strings: Record<string, string> = { 'units.classesCredits': '{{ classes }} classes ({{ credits }} credits)', 'units.classCredits': '1 class ({{ credits }} credits)' };
  const t = (k: string, p?: Record<string, unknown>) => (strings[k] ?? k).replace(/\{\{ (\w+) \}\}/g, (_, n: string) => String(p?.[n] ?? ''));

  it('shows "5 classes (15 credits)" and the singular', () => {
    expect(classesAndCredits(5, 15, t, 'en')).toBe('5 classes (15 credits)');
    expect(classesAndCredits(1, 3, t, 'en')).toBe('1 class (3 credits)');
    expect(classesFromCredits(21)).toBe(7);
    expect(classesFromCredits(12)).toBe(4);
  });

  it('counts classes left from credits when every course is 3 credits, otherwise exactly', () => {
    expect(classesLeft(105, [3, 3, 3], 31, 4)).toBe(35);
    expect(classesLeft(104, [3, 4, 3], 31, 4)).toBe(35); // a 4-credit course: count the classes themselves
    expect(classesLeft(104, [3, 4, 3], 30, 4)).toBe(34);
  });
});