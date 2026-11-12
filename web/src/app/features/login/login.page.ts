import { Component, ElementRef, afterNextRender, computed, inject, input, signal, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService, SignInError, SsoConfig } from '../../core/auth/auth.service';
import { TourService, TourStop } from '../../core/tour/tour.service';
import { SHOWCASE } from '../../showcase/showcase';
import { WalkthroughService } from '../../core/tour/walkthrough';
import { LanguageService } from '../../core/i18n/language.service';

const SSO_ERRORS = ['failed', 'not_registered', 'inactive', 'expired', 'unavailable'] as const;
type SsoError = (typeof SSO_ERRORS)[number];

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, TranslocoPipe],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  /** Bound from the query string. */
  readonly returnUrl = input<string>();
  readonly reason = input<string>();
  /** One-time code from the server after Microsoft sign-in (?sso_code=…). */
  readonly ssoCode = input<string | undefined>(undefined, { alias: 'sso_code' });
  /** Why a Microsoft sign-in didn't finish (?sso=failed|not_registered|inactive). */
  readonly sso = input<string>();

  protected readonly ssoConfig = signal<SsoConfig | null>(null);
  protected readonly completing = signal(false);
  protected readonly redirecting = signal(false);
  protected readonly showLocal = signal(false);
  private readonly ssoExpired = signal(false);

  /** A plain-language message for a Microsoft sign-in that didn't finish, if any. */
  protected readonly ssoError = computed<SsoError | null>(() => {
    if (this.ssoExpired()) return 'expired';
    const reason = this.sso();
    return reason && (SSO_ERRORS as readonly string[]).includes(reason) ? (reason as SsoError) : null;
  });

  /** The username form shows when SSO is off, or when someone chooses it. */
  protected readonly localVisible = computed(() => {
    const config = this.ssoConfig();
    if (!config) return false;
    return !config.enabled || (config.localSignInEnabled && this.showLocal());
  });

  constructor() {
    afterNextRender(() => void this.init());
  }

  private async init(): Promise<void> {
    const code = this.ssoCode();
    if (code) {
      this.completing.set(true);
      const result = await this.auth.completeSso(code);
      if (result.ok) {
        await this.router.navigateByUrl(this.safe(result.returnUrl) ?? this.auth.homeUrl(), { replaceUrl: true });
        return;
      }
      this.ssoExpired.set(true);
      this.completing.set(false);
      // Drop the used code from the address bar.
      await this.router.navigate([], { queryParams: { sso_code: null }, queryParamsHandling: 'merge', replaceUrl: true });
    }
    this.ssoConfig.set(await this.auth.ssoConfig());
    // Practice accounts: the API only answers in Development with demo data on (404 everywhere else).
    await this.tour.load();
  }

  private readonly tour = inject(TourService);
  private readonly walk = inject(WalkthroughService);
  protected readonly demo = this.tour.demo;
  protected readonly startingTour = signal(false);
  protected readonly showcase: boolean = SHOWCASE;

  protected useDemo(username: string): void {
    const d = this.demo();
    if (!d) return;
    this.showLocal.set(true);
    this.form.setValue({ username, password: username === 'admin' ? d.adminPassword : d.password });
  }

  /** Look around without the tour: each opens the platform as that practice account. */
  protected readonly explorers: TourStop[] = [
    { key: 'student', icon: 'school', username: 'student.hanieh', link: '/student' },
    { key: 'advisor', icon: 'supervisor_account', username: 'advisor.it', link: '/advisor' },
    { key: 'admin', icon: 'admin_panel_settings', username: 'admin', link: '/admin' },
  ];

  protected async explore(e: TourStop): Promise<void> {
    if (this.startingTour()) return;
    this.startingTour.set(true);
    const ok = await this.tour.visit(e);
    this.startingTour.set(false);
    if (!ok) this.error.set('unavailable');
  }

  /** One click for a visitor: signs in as the demo admin and opens the guided tour. */
  protected async startTour(): Promise<void> {
    const d = this.demo();
    if (!d || this.startingTour()) return;
    this.startingTour.set(true);
    const result = await this.auth.signIn('admin', d.adminPassword);
    this.startingTour.set(false);
    if (result.ok) {
      await this.walk.start(0);
    } else this.error.set(result.error);
  }

  protected startSso(): void {
    this.redirecting.set(true);
    this.auth.startSso(this.safeReturnUrl());
  }

  protected readonly form = inject(NonNullableFormBuilder).group({
    username: ['', [Validators.required, Validators.maxLength(100)]],
    password: ['', [Validators.required, Validators.maxLength(200)]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<SignInError | null>(null);
  protected readonly showPassword = signal(false);
  private readonly usernameInput = viewChild.required<ElementRef<HTMLInputElement>>('usernameInput');

  protected async submit(): Promise<void> {
    if (this.submitting()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.usernameInput().nativeElement.focus();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    const { username, password } = this.form.getRawValue();
    const result = await this.auth.signIn(username, password);
    this.submitting.set(false);

    if (result.ok) {
      // On a demo server, signing in as admin opens the guided tour.
      if (!this.safeReturnUrl() && (await this.tour.isTourSignIn(username))) {
        await this.walk.start(0);
        return;
      }
      const target = this.safeReturnUrl() ?? this.auth.homeUrl();
      await this.router.navigateByUrl(target);
    } else {
      this.error.set(result.error);
      this.form.controls.password.reset();
    }
  }

  private safeReturnUrl(): string | null {
    return this.safe(this.returnUrl());
  }

  /** Only same-app paths; never an absolute URL (prevents open redirects). */
  private safe(url: string | null | undefined): string | null {
    return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\') && !url.startsWith('/login') ? url : null;
  }
}
