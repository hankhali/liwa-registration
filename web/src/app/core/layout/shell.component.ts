import { A11yModule } from '@angular/cdk/a11y';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { termLabel } from '../planning/planning.models';
import { filter, firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { LanguageService } from '../i18n/language.service';
import { TranslatedTitleStrategy } from '../i18n/translated-title.strategy';
import { SHOWCASE } from '../../showcase/showcase';
import { TourService } from '../tour/tour.service';
import { WalkthroughService } from '../tour/walkthrough';
import { WalkthroughOverlayComponent } from '../tour/walkthrough-overlay.component';
import { NAV, NavItem } from './nav';
import { NotificationBellComponent } from './notification-bell.component';
import { ThemeChoice, ThemeService } from './theme.service';

interface TermOption {
  code: string;
  name: string;
  isCurrent: boolean;
}

const RAIL_KEY = 'liwa.railCollapsed';

/**
 * The app frame (brief Â§9): indigo top bar with the white wordmark, page title, term selector, notifications, EN/AR and the
 * user menu; a collapsible left rail per role; on phones a bottom bar with the main items and a drawer with all.
 */
@Component({
  selector: 'app-shell',
  imports: [
    A11yModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatBadgeModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatSelectModule,
    TranslocoPipe,
    NotificationBellComponent,
    WalkthroughOverlayComponent,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly theme = inject(ThemeService);
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);
  private readonly titles = inject(TranslatedTitleStrategy);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  protected readonly nav = computed<NavItem[]>(() => {
    const role = this.auth.role();
    return role ? NAV[role] : [];
  });
  /** Items with a bottom-bar label (students), otherwise the first four. */
  protected readonly bottomNav = computed(() => {
    const marked = this.nav().filter((i) => i.bottom);
    return marked.length > 0 ? marked : this.nav().slice(0, 4);
  });
  /** Students' bottom bar has exactly four items; other roles add "More" (the drawer). */
  protected readonly bottomMore = computed(() => !this.nav().some((i) => i.bottom));
  protected readonly pageTitle = this.titles.pageKey;
  protected readonly collapsed = signal(this.readCollapsed());
  protected readonly drawerOpen = signal(false);
  protected readonly terms = signal<TermOption[]>([]);
  protected readonly currentTerm = computed(() => this.terms().find((t) => t.isCurrent) ?? null);
  protected readonly badges = signal<Record<string, number>>({});
  protected readonly themes: ThemeChoice[] = ['system', 'light', 'dark'];
  protected readonly tour = inject(TourService);
  private readonly router = inject(Router);
  private readonly url = signal('');
  /** During the guided tour, everywhere except the tour page: the name of the account being viewed. */
  protected readonly walk = inject(WalkthroughService);
  protected readonly showcase: boolean = SHOWCASE;
  protected readonly tourBar = computed(() =>
    this.tour.touring() && this.walk.index() === null && !this.url().startsWith('/tour') ? (this.auth.user()?.displayName ?? null) : null,
  );

  constructor() {
    // After navigation, move focus to the main region so screen-reader and keyboard users
    // start at the new page content rather than the top of the toolbar (WCAG 2.4.3).
    let first = true;
    this.url.set(this.router.url);
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe((e) => {
        this.url.set(e.urlAfterRedirects);
        this.drawerOpen.set(false);
        void this.loadBadges();
        if (first) {
          first = false;
          return;
        }
        queueMicrotask(() => this.main().nativeElement.focus({ preventScroll: true }));
      });

    // Remember the language on the server too, so emails come in the same language (brief Â§4).
    effect(() => {
      const lang = this.language.lang();
      if (this.auth.isSignedIn()) void firstValueFrom(this.http.put('/api/me/language', { language: lang })).catch(() => undefined);
    });

    void this.loadTerms();
    const timer = setInterval(() => void this.loadBadges(), 60_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected initials(name: string): string {
    const parts = name.replace(/^Dr\.?\s+/i, '').split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
  }

  /** The semester name in the reader's language ("Spring 2027" / "الربيع 2027"). */
  protected termName(t: TermOption): string {
    return termLabel(t.code, (k) => this.transloco.translate(k), this.language.lang()) || t.name;
  }

  protected toggleRail(): void {
    this.collapsed.update((c) => !c);
    try {
      localStorage.setItem(RAIL_KEY, String(this.collapsed()));
    } catch {
      /* not remembered */
    }
  }

  protected signOut(): void {
    this.walk.stop();
    this.tour.end();
    this.auth.signOut('user');
  }

  protected backToTour(): void {
    void this.tour.back();
  }

  private readCollapsed(): boolean {
    try {
      return localStorage.getItem(RAIL_KEY) === 'true';
    } catch {
      return false;
    }
  }

  private async loadTerms(): Promise<void> {
    try {
      this.terms.set(await firstValueFrom(this.http.get<TermOption[]>('/api/me/terms')));
    } catch {
      /* the selector just stays empty */
    }
  }

  private async loadBadges(): Promise<void> {
    if (this.auth.role() !== 'Advisor') return;
    try {
      const o = await firstValueFrom(this.http.get<{ awaitingReview: number; needsReview: number }>('/api/advisor/overview'));
      this.badges.set({ queue: o.awaitingReview + o.needsReview });
    } catch {
      /* keep the last count */
    }
  }
}
