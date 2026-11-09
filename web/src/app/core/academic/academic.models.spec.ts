import { describe, expect, it } from 'vitest';
import { formatDateTime, formatGpa, percentOf, programName } from './academic.models';

describe('academic display helpers', () => {
  it('shows the program name in Arabic when available, otherwise English', () => {
    const p = { programName: 'Bachelor of Information Technology', programNameAr: 'بكالوريوس تقنية المعلومات' };
    expect(programName(p, 'ar')).toBe('بكالوريوس تقنية المعلومات');
    expect(programName(p, 'en')).toBe('Bachelor of Information Technology');
    expect(programName({ ...p, programNameAr: null }, 'ar')).toBe('Bachelor of Information Technology');
  });

  it('rounds progress to a whole percent and never passes 100', () => {
    expect(percentOf(36, 120)).toBe(30);
    expect(percentOf(114, 120)).toBe(95);
    expect(percentOf(130, 120)).toBe(100);
    expect(percentOf(5, 0)).toBe(0);
  });

  it('always shows two decimals for a GPA', () => {
    expect(formatGpa(3.5)).toBe('3.50');
    expect(formatGpa(1.4)).toBe('1.40');
  });

  it('shows times in 12-hour format, never 24-hour', () => {
    const text = formatDateTime('2027-01-11T13:05:00Z', 'en');
    expect(text).toMatch(/\b(am|pm)\b/); // lower-case, as in "5:00 pm"
    expect(text).not.toMatch(/\b1[3-9]:\d\d|2[0-3]:\d\d/);
  });
});
