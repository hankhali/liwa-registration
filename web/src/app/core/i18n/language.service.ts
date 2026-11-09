import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

export type Lang = 'en' | 'ar';
export type Dir = 'ltr' | 'rtl';

export const SUPPORTED_LANGS: readonly Lang[] = ['en', 'ar'];
const STORAGE_KEY = 'liwa.lang';

/** Current UI language and text direction. Keeps <html lang/dir> and Transloco in sync. */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly document = inject(DOCUMENT);
  private readonly transloco = inject(TranslocoService);

  readonly lang = signal<Lang>(this.initialLang());
  readonly dir = computed<Dir>(() => (this.lang() === 'ar' ? 'rtl' : 'ltr'));
  /** The language you would switch to. */
  readonly other = computed<Lang>(() => (this.lang() === 'ar' ? 'en' : 'ar'));

  constructor() {
    effect(() => {
      const lang = this.lang();
      const html = this.document.documentElement;
      html.lang = lang;
      html.dir = this.dir();
      this.transloco.setActiveLang(lang);
      try {
        localStorage.setItem(STORAGE_KEY, lang);
      } catch {
        /* storage unavailable (private mode): language just isn't remembered */
      }
    });
  }

  set(lang: Lang): void {
    this.lang.set(lang);
  }

  toggle(): void {
    this.lang.set(this.other());
  }

  private initialLang(): Lang {
    const fromUrl = new URLSearchParams(this.document.location?.search ?? '').get('lang');
    if (isLang(fromUrl)) return fromUrl;
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (isLang(saved)) return saved;
    } catch {
      /* ignore */
    }
    const browser = this.document.defaultView?.navigator.language ?? 'en';
    return browser.toLowerCase().startsWith('ar') ? 'ar' : 'en';
  }
}

function isLang(value: string | null | undefined): value is Lang {
  return value === 'en' || value === 'ar';
}
