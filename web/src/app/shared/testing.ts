import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentProviders, Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { LanguageService } from '../core/i18n/language.service';

/** TestBed with the real English and Arabic translations, so tests see what users see. */
export function setupI18n(lang: 'en' | 'ar' = 'en', providers: (Provider | EnvironmentProviders)[] = []): void {
  try {
    localStorage.setItem('liwa.lang', lang);
  } catch {
    /* ignore */
  }
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: { en, ar },
        translocoConfig: { availableLangs: ['en', 'ar'], defaultLang: lang },
        preloadLangs: true,
      }),
    ],
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), ...providers],
  });
  TestBed.inject(LanguageService).set(lang);
}
