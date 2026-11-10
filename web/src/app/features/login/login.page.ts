import { Component, ElementRef, afterNextRender, computed, inject, input, signal, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService, SignInError, SsoConfig } from '../../core/auth/auth.service';

interface DemoAccounts {
  password: string;
  accounts: { username: string; role: string; description: string }[];
}
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
    try {
      this.demo.set(await firstValueFrom(this.http.get<DemoAccounts>('/api/auth/demo-accounts')));
    } catch {
      this.demo.set(null);
    }
  }

  protected readonly demo = signal<DemoAccounts | null>(null);
  private readonly http = inject(HttpClient);

  protected useDemo(username: string, password: string): void {
    this.showLocal.set(true);
    this.form.setValue({ username, password });
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
