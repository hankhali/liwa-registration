import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { setArabicDigits } from './core/i18n/digits';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { TitleStrategy, provideRouter, withComponentInputBinding, withHashLocation, withInMemoryScrolling } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { LanguageService } from './core/i18n/language.service';
import { provideLiwaI18n } from './core/i18n/transloco';
import { TranslatedTitleStrategy } from './core/i18n/translated-title.strategy';
import { ThemeService } from './core/layout/theme.service';
import { SHOWCASE } from './showcase/showcase';
import { loadShowcaseData, showcaseInterceptor } from './showcase/showcase.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Showcase build: the recorded answers stand in for the API, and page addresses use # (static hosting).
    provideHttpClient(withInterceptors(SHOWCASE ? [authInterceptor, showcaseInterceptor] : [authInterceptor])),
    SHOWCASE
      ? provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'top' }), withHashLocation())
      : provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideLiwaI18n(),
    // useExisting: the shell injects the same instance to show the page title in the top bar.
    { provide: TitleStrategy, useExisting: TranslatedTitleStrategy },
    // Load the starting language before first render so no untranslated keys flash on screen.
    provideAppInitializer(async () => {
      inject(ThemeService); // apply the saved light/dark choice everywhere, including the sign-in page
      const http = inject(HttpClient);
      const lang = inject(LanguageService).lang();
      const transloco = inject(TranslocoService);
      await loadShowcaseData();
      // Deployment-wide digits setting (UI_ARABIC_DIGITS); don't block start-up on it.
      void firstValueFrom(http.get<{ arabicDigits?: boolean }>('/api/auth/sso/config'))
        .then((c) => setArabicDigits(!!c.arabicDigits))
        .catch(() => undefined);
      await firstValueFrom(transloco.load(lang));
    }),
  ],
};
