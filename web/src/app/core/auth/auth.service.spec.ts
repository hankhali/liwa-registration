import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { Session } from './auth.models';
import { AuthService } from './auth.service';
import { HttpClient } from '@angular/common/http';

const user = { id: 'u1', username: 'student.new', displayName: 'Mariam Al Mansoori', role: 'Student' as const, universityId: '202700101' };

function setup(stored?: Session) {
  localStorage.clear();
  if (stored) localStorage.setItem('liwa.session', JSON.stringify(stored));
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
  });
  const router = TestBed.inject(Router);
  const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
  return { auth: TestBed.inject(AuthService), http: TestBed.inject(HttpTestingController), client: TestBed.inject(HttpClient), navigate };
}

describe('AuthService', () => {
  beforeEach(() => TestBed.resetTestingModule());
  afterEach(() => localStorage.clear());

  it('signs in, exposes the user and role home, and remembers the session', async () => {
    const { auth, http } = setup();
    const pending = auth.signIn('student.new', 'Liwa#2027');
    const req = http.expectOne('/api/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ accessToken: 'tok', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), user });

    expect(await pending).toEqual({ ok: true });
    expect(auth.user()?.displayName).toBe('Mariam Al Mansoori');
    expect(auth.homeUrl()).toBe('/student');
    expect(JSON.parse(localStorage.getItem('liwa.session')!).token).toBe('tok');
  });

  it.each([
    [401, 'invalid'],
    [429, 'throttled'],
    [503, 'unavailable'],
  ])('maps a %s sign-in response to "%s"', async (status, expected) => {
    const { auth, http } = setup();
    const pending = auth.signIn('x', 'y');
    http.expectOne('/api/auth/login').flush({}, { status, statusText: 'x' });
    expect(await pending).toEqual({ ok: false, error: expected });
    expect(auth.isSignedIn()).toBe(false);
  });

  it('restores a valid stored session', () => {
    const { auth } = setup({ token: 'tok', expiresAt: Date.now() + 60_000, user });
    expect(auth.isSignedIn()).toBe(true);
    expect(auth.token()).toBe('tok');
  });

  it('discards an expired stored session', () => {
    const { auth } = setup({ token: 'tok', expiresAt: Date.now() - 1, user });
    expect(auth.isSignedIn()).toBe(false);
    expect(localStorage.getItem('liwa.session')).toBeNull();
  });

  it('discards a corrupted stored session', () => {
    localStorage.setItem('liwa.session', '{not json');
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    expect(TestBed.inject(AuthService).isSignedIn()).toBe(false);
  });

  it('adds the bearer token to API calls', () => {
    const { client, http } = setup({ token: 'tok', expiresAt: Date.now() + 60_000, user });
    client.get('/api/auth/me').subscribe();
    expect(http.expectOne('/api/auth/me').request.headers.get('Authorization')).toBe('Bearer tok');
  });

  it('does not send the token to other origins', () => {
    const { client, http } = setup({ token: 'tok', expiresAt: Date.now() + 60_000, user });
    client.get('https://example.com/api/x').subscribe();
    expect(http.expectOne('https://example.com/api/x').request.headers.has('Authorization')).toBe(false);
  });

  it('signs out and goes to sign-in when an API call returns 401', () => {
    const { auth, client, http, navigate } = setup({ token: 'tok', expiresAt: Date.now() + 60_000, user });
    client.get('/api/auth/me').subscribe({ error: () => undefined });
    http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isSignedIn()).toBe(false);
    expect(localStorage.getItem('liwa.session')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login'], expect.objectContaining({ queryParams: expect.objectContaining({ reason: 'expired' }) }));
  });

  it('signs out when the token expires', () => {
    vi.useFakeTimers();
    try {
      const { auth } = setup({ token: 'tok', expiresAt: Date.now() + 1_000, user });
      expect(auth.isSignedIn()).toBe(true);
      vi.advanceTimersByTime(1_001);
      expect(auth.isSignedIn()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });
  describe('Sign in with your Liwa account', () => {
    afterEach(() => sessionStorage.clear());

    it('reads the sign-in options', async () => {
      const { auth, http } = setup();
      const pending = auth.ssoConfig();
      http.expectOne('/api/auth/sso/config').flush({ enabled: true, localSignInEnabled: false });
      expect(await pending).toEqual({ enabled: true, localSignInEnabled: false });
    });

    it('falls back to the username form when the options cannot be loaded', async () => {
      const { auth, http } = setup();
      const pending = auth.ssoConfig();
      http.expectOne('/api/auth/sso/config').flush({}, { status: 503, statusText: 'x' });
      expect(await pending).toEqual({ enabled: false, localSignInEnabled: true });
    });

    it('redeems the one-time code, starts the session and returns the remembered page once', async () => {
      const { auth, http } = setup();
      sessionStorage.setItem('liwa.returnUrl', '/student');
      const pending = auth.completeSso('one-time-code');
      const req = http.expectOne('/api/auth/sso/exchange');
      expect(req.request.body).toEqual({ code: 'one-time-code' });
      req.flush({ accessToken: 'tok', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), user });

      expect(await pending).toEqual({ ok: true, returnUrl: '/student' });
      expect(auth.user()?.username).toBe('student.new');
      expect(sessionStorage.getItem('liwa.returnUrl')).toBeNull();
    });

    it.each([
      [400, 'expired'],
      [503, 'unavailable'],
    ])('maps a %s code exchange to "%s" and stays signed out', async (status, expected) => {
      const { auth, http } = setup();
      const pending = auth.completeSso('used-code');
      http.expectOne('/api/auth/sso/exchange').flush({}, { status, statusText: 'x' });
      expect(await pending).toEqual({ ok: false, error: expected });
      expect(auth.isSignedIn()).toBe(false);
    });
  });
});
