/**
 * Digits (brief §4): Western 0–9 in both languages by default; one deployment-wide setting, UI_ARABIC_DIGITS,
 * switches the Arabic UI to Arabic-Indic digits (٠–٩). Loaded once at start-up from /api/auth/sso/config.
 */
let arabicDigits = false;

export function setArabicDigits(on: boolean): void {
  arabicDigits = on;
}

export function useArabicDigits(lang: string): boolean {
  return lang === 'ar' && arabicDigits;
}

const ARABIC_INDIC = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];

/** Converts 0–9 to the digits the deployment uses for this language. */
export function localDigits(text: string, lang: string): string {
  return useArabicDigits(lang) ? text.replace(/\d/g, (d) => ARABIC_INDIC[Number(d)]) : text;
}

/** Intl locale for dates: Gregorian calendar, Arabic month names, digits per the setting. */
export function dateLocale(lang: string): string {
  return lang === 'ar' ? (useArabicDigits(lang) ? 'ar-AE-u-ca-gregory-nu-arab' : 'ar-AE-u-ca-gregory-nu-latn') : 'en-AE';
}
