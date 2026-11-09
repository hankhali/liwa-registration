import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { HOME_BY_ROLE, Role, Session, SignInResponse, SignOutReason } from './auth.models';

const STORAGE_KEY = 'liwa.session';

export type SignInError = 'invalid' | 'throttled' | 'unavailable';

/** Which sign-in options the server offers. */
export interface SsoConfig {
  enabled: boolean;
  localSignInEnabled: boolean;
}

export const SSO_START_URL = '/api/auth/sso/start';
const RETURN_URL_KEY = 'liwa.returnUrl';

/**
 * The single source of truth for who is signed in. Components read signals; only this service
 * touches storage. When SSO arrives, a second sign-in method is added here and nothing else changes.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  private readonly session = signal<Session | null>(null);

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);
  readonly role = computed<Role | null>(() => this.session()?.user.role ?? null);
  readonly homeUrl = computed(() => {
    const role = this.role();
    return role ? HOME_BY_ROLE[role] : '/login';
  });

  constructor() {
    this.restore();
    inject(DestroyRef).onDestroy(() => clearTimeout(this.expiryTimer));
  }

  token(): string | null {
    const s = this.session();
    if (!s) return null;
    if (s.expiresAt <= Date.now()) {
      this.signOut('expired');
      return null;
    }
    return s.token;
  }

  async signIn(username: string, password: string): Promise<{ ok: true } | { ok: false; error: SignInError }> {
    try {
      const res = await firstValueFrom(this.http.post<SignInResponse>('/api/auth/login', { username, password }));
      this.start({ token: res.accessToken, expiresAt: Date.parse(res.expiresAt), user: res.user });
      return { ok: true };
    } catch (e) {
      const status = e instanceof HttpErrorResponse ? e.status : 0;
      return { ok: false, error: status === 401 || status === 400 ? 'invalid' : status === 429 ? 'throttled' : 'unavailable' };
    }
  }

  /** Sign-in options. If the server can't be reached, fall back to the username form. */
  async ssoConfig(): Promise<SsoConfig> {
    try {
      return await firstValueFrom(this.http.get<SsoConfig>('/api/auth/sso/config'));
    } catch {
      return { enabled: false, localSignInEnabled: true };
    }
  }

  /** Leaves the app for the university Microsoft sign-in page. Remembers where to return afterwards. */
  startSso(returnUrl: string | null): void {
    try {
      if (returnUrl) sessionStorage.setItem(RETURN_URL_KEY, returnUrl);
      else sessionStorage.removeItem(RETURN_URL_KEY);
    } catch {
      /* ignore: we'll land on the home page */
    }
    this.document.location.assign(SSO_START_URL);
  }

  /** Redeems the one-time code the server returned after Microsoft sign-in. */
  async completeSso(code: string): Promise<{ ok: true; returnUrl: string | null } | { ok: false; error: 'expired' | 'unavailable' }> {
    try {
      const res = await firstValueFrom(this.http.post<SignInResponse>('/api/auth/sso/exchange', { code }));
      this.start({ token: res.accessToken, expiresAt: Date.parse(res.expiresAt), user: res.user });
      return { ok: true, returnUrl: this.takeReturnUrl() };
    } catch (e) {
      const status = e instanceof HttpErrorResponse ? e.status : 0;
      return { ok: false, error: status === 400 ? 'expired' : 'unavailable' };
    }
  }

  private takeReturnUrl(): string | null {
    try {
      const url = sessionStorage.getItem(RETURN_URL_KEY);
      sessionStorage.removeItem(RETURN_URL_KEY);
      return url;
    } catch {
      return null;
    }
  }

  signOut(reason: SignOutReason = 'user', returnUrl?: string): void {
    const wasSignedIn = this.session() !== null;
    this.session.set(null);
    clearTimeout(this.expiryTimer);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
    if (wasSignedIn || reason === 'expired') {
      const queryParams: Record<string, string> = {};
      if (reason === 'expired') queryParams['reason'] = 'expired';
      if (returnUrl && returnUrl !== '/' && !returnUrl.startsWith('/login')) queryParams['returnUrl'] = returnUrl;
      void this.router.navigate(['/login'], { queryParams });
    }
  }

  private start(session: Session): void {
    this.session.set(session);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      /* ignore: session lasts for this tab only */
    }
    this.scheduleExpiry(session.expiresAt);
  }

  private restore(): void {
    let raw: string | null = null;
    try {
      raw = localStorage.getItem(STORAGE_KEY);
    } catch {
      return;
    }
    if (!raw) return;
    try {
      const s = JSON.parse(raw) as Session;
      if (s?.token && s.user?.role && s.expiresAt > Date.now()) {
        this.session.set(s);
        this.scheduleExpiry(s.expiresAt);
        return;
      }
    } catch {
      /* corrupted: fall through and clear */
    }
    localStorage.removeItem(STORAGE_KEY);
  }

  private scheduleExpiry(expiresAt: number): void {
    clearTimeout(this.expiryTimer);
    const ms = expiresAt - Date.now();
    // setTimeout overflows above ~24.8 days; tokens are hours long, so cap defensively.
    this.expiryTimer = setTimeout(() => this.signOut('expired', this.router.url), Math.min(Math.max(ms, 0), 2 ** 31 - 1));
  }
}
