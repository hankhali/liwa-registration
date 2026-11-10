import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { setArabicDigits } from './core/i18n/digits';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { TitleStrategy, provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { LanguageService } from './core/i18n/language.service';
import { provideLiwaI18n } from './core/i18n/transloco';
import { TranslatedTitleStrategy } from './core/i18n/translated-title.strategy';
import { ThemeService } from './core/layout/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideLiwaI18n(),
    // useExisting: the shell injects the same instance to show the page title in the top bar.
    { provide: TitleStrategy, useExisting: TranslatedTitleStrategy },
    // Load the starting language before first render so no untranslated keys flash on screen.
    provideAppInitializer(() => {
      inject(ThemeService); // apply the saved light/dark choice everywhere, including the sign-in page
      // Deployment-wide digits setting (UI_ARABIC_DIGITS); don't block start-up on it.
      void firstValueFrom(inject(HttpClient).get<{ arabicDigits?: boolean }>('/api/auth/sso/config'))
        .then((c) => setArabicDigits(!!c.arabicDigits))
        .catch(() => undefined);
      const lang = inject(LanguageService).lang();
      return firstValueFrom(inject(TranslocoService).load(lang));
    }),
  ],
};
