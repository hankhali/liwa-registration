/** Unicode "left-to-right isolate" and "pop directional isolate" (brief §9: isolate only the course code). */
export const LRI = '⁦';
export const PDI = '⁩';

/** A course code that will never be reordered by the surrounding Arabic text: ⁦ITC201⁩. */
export function isolate(code: string): string {
  return code ? `${LRI}${code}${PDI}` : code;
}

/** True when a string looks like a course code (2–4 letters + 3 digits, optional L). */
export function isCourseCode(s: string): boolean {
  return /^[A-Z]{2,4}\d{3}L?$/.test(s);
}
