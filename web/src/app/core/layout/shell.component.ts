import { Component, ElementRef, inject, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../auth/auth.service';
import { LanguageService } from '../i18n/language.service';
import { NotificationBellComponent } from './notification-bell.component';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, MatButtonModule, MatIconModule, MatMenuModule, TranslocoPipe, NotificationBellComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  constructor() {
    // After navigation, move focus to the main region so screen-reader and keyboard users
    // start at the new page content rather than the top of the toolbar (WCAG 2.4.3).
    let first = true;
    inject(Router)
      .events.pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => {
        if (first) {
          first = false;
          return;
        }
        queueMicrotask(() => this.main().nativeElement.focus({ preventScroll: true }));
      });
  }

  protected initials(name: string): string {
    const parts = name.replace(/^Dr\.?\s+/i, '').split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
  }

  protected signOut(): void {
    this.auth.signOut('user');
  }
}
