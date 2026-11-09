import { describe, expect, it } from 'vitest';
import { PRESETS, blockedGroup, formatTime, meetingPatterns, parseTermCode, warningKey, windowsFromGrid } from './planning.models';

describe('planning helpers', () => {
  it('shows times in 12-hour format in both languages', () => {
    expect(formatTime('17:00:00', 'en')).toBe('5:00 pm');
    expect(formatTime('09:05:00', 'en')).toBe('9:05 am');
    expect(formatTime('12:30:00', 'en')).toBe('12:30 pm');
    expect(formatTime('17:00:00', 'ar')).toBe('5:00 م');
  });

  it('merges neighbouring free periods so a class across noon still fits', () => {
    const windows = windowsFromGrid({ 'Monday.morning': true, 'Monday.afternoon': true, 'Tuesday.evening': true });
    expect(windows).toEqual([
      { day: 'Monday', start: '08:00', end: '17:00' },
      { day: 'Tuesday', start: '17:00', end: '20:00' },
    ]);
  });

  it('never offers Friday afternoon or evening (Jumu\'ah)', () => {
    expect(windowsFromGrid({ 'Friday.afternoon': true, 'Friday.evening': true })).toEqual([]);
    expect(PRESETS.daytime.find((w) => w.day === 'Friday')!.end).toBe('12:00');
  });

  it('the evenings preset is Monday to Thursday, 5 to 8 pm', () => {
    expect(PRESETS.evenings.map((w) => w.day)).toEqual(['Monday', 'Tuesday', 'Wednesday', 'Thursday']);
    expect(PRESETS.evenings.every((w) => w.start === '17:00' && w.end === '20:00')).toBe(true);
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

  it('reads term codes', () => {
    expect(parseTermCode('2027FA')).toEqual({ season: 'Fall', year: 2027 });
    expect(parseTermCode('bad')).toBeNull();
  });
});
