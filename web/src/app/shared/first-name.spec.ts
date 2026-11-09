import { describe, expect, it } from 'vitest';
import { greetingName } from './first-name';

describe('greetingName', () => {
  it.each([
    ['Mariam Al Mansoori', 'Mariam'],
    ['Dr. Ahmed Al Zaabi', 'Dr. Ahmed'],
    ['Registrar Admin', 'Registrar'],
    ['  Omar  ', 'Omar'],
  ])('%s → %s', (input, expected) => expect(greetingName(input)).toBe(expected));
});
