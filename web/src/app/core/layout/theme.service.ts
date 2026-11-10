import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export type ThemeChoice = 'system' | 'light' | 'dark';
const STORAGE_KEY = 'liwa.theme';

/** Light / dark / follow the device. Only sets <html data-theme>; all colours are CSS tokens (styles.scss). */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  readonly choice = signal<ThemeChoice>(this.initial());

  constructor() {
    effect(() => {
      const c = this.choice();
      const html = this.document.documentElement;
      if (c === 'system') html.removeAttribute('data-theme');
      else html.setAttribute('data-theme', c);
      try {
        localStorage.setItem(STORAGE_KEY, c);
      } catch {
        /* not remembered */
      }
    });
  }

  private initial(): ThemeChoice {
    try {
      const v = localStorage.getItem(STORAGE_KEY);
      if (v === 'light' || v === 'dark' || v === 'system') return v;
    } catch {
      /* ignore */
    }
    return 'system';
  }
}
