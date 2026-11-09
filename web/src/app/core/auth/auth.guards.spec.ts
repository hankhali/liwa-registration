import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { authGuard, guestGuard, roleGuard } from './auth.guards';
import { Role } from './auth.models';

function signedInAs(role: Role | null) {
  localStorage.clear();
  if (role) {
    localStorage.setItem(
      'liwa.session',
      JSON.stringify({ token: 't', expiresAt: Date.now() + 60_000, user: { id: '1', username: 'u', displayName: 'U', role, universityId: null } }),
    );
  }
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
}

function run(guard: (r: ActivatedRouteSnapshot, s: RouterStateSnapshot) => unknown, url = '/student') {
  return runInInjectionContext(TestBed.inject(EnvironmentInjector), () =>
    guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  );
}

const asUrl = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

describe('route guards', () => {
  beforeEach(() => localStorage.clear());

  it('authGuard sends signed-out users to sign-in with a return URL', () => {
    signedInAs(null);
    expect(asUrl(run(authGuard, '/advisor'))).toBe('/login?returnUrl=%2Fadvisor');
  });

  it('authGuard lets signed-in users through', () => {
    signedInAs('Student');
    expect(run(authGuard)).toBe(true);
  });

  it('roleGuard allows the matching role', () => {
    signedInAs('Advisor');
    expect(run(roleGuard('Advisor'), '/advisor')).toBe(true);
  });

  it.each<Role>(['Student', 'Advisor'])('roleGuard sends a %s away from admin pages', (role) => {
    signedInAs(role);
    expect(asUrl(run(roleGuard('Admin'), '/admin'))).toBe('/forbidden');
  });

  it('guestGuard sends signed-in users to their own home', () => {
    signedInAs('Admin');
    expect(asUrl(run(guestGuard, '/login'))).toBe('/admin');
  });
});
