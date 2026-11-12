import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { WALK_PARTS, WALK_STEPS, WalkthroughService } from './walkthrough';

const demo = { password: 'Liwa#2027', adminPassword: 'admin', accounts: [] };
const signedIn = (username: string, role: string) => ({
  accessToken: 't-' + username,
  expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  user: { id: username, username, displayName: username, role, universityId: null },
});
const tick = () => new Promise((r) => setTimeout(r));

function setup() {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  const walk = TestBed.inject(WalkthroughService);
  walk.waitMs = 0;
  return { walk, http: TestBed.inject(HttpTestingController), navigate };
}

describe('Guided walkthrough', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    sessionStorage.clear();
    localStorage.clear();
  });

  it('tells one story in four parts, each opened by an introduction card', () => {
    expect(WALK_STEPS[0].kind).toBe('scene');
    expect(WALK_STEPS.at(-1)!.kind).toBe('end');
    for (let p = 1; p <= WALK_PARTS; p++) {
      const steps = WALK_STEPS.filter((s) => s.part === p);
      expect(steps[0].kind).toBe('scene');
      expect(steps.length).toBeGreaterThan(1);
      // A part has a single practice account.
      expect(new Set(steps.map((s) => s.account)).size).toBe(1);
    }
    // Steps without a page always follow a click (or another such step) on the same page.
    WALK_STEPS.forEach((s, i) => {
      if (!s.route) expect(['click', 'look']).toContain(WALK_STEPS[i - 1].kind);
    });
  });

  it('signs in as the right account and opens the page for a step', async () => {
    const { walk, http, navigate } = setup();
    const studentIntro = WALK_STEPS.findIndex((s) => s.id === 'studentIntro');
    const done = walk.start(studentIntro);
    http.expectOne('/api/auth/demo-accounts').flush(demo);
    await tick();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.body).toEqual({ username: 'student.hanieh', password: 'Liwa#2027' });
    login.flush(signedIn('student.hanieh', 'Student'));
    await done;
    expect(navigate).toHaveBeenCalledWith('/student');
    expect(walk.step()!.id).toBe('studentIntro');
    expect(walk.position()).toMatchObject({ part: 1, parts: 4 });
    expect(sessionStorage.getItem('liwa.walkStep')).toBe(String(studentIntro));
  });

  it('goes back on the same page without reopening it, and stops cleanly', async () => {
    const { walk, http, navigate } = setup();
    const decide = WALK_STEPS.findIndex((s) => s.id === 'decide');
    const start = walk.start(decide);
    http.expectOne('/api/auth/demo-accounts').flush(demo);
    await tick();
    http.expectOne('/api/auth/login').flush(signedIn('advisor.it', 'Advisor'));
    await start;
    navigate.mockClear();
    await walk.back();
    expect(walk.step()!.id).toBe('reviewGrad');
    expect(navigate).not.toHaveBeenCalled();

    walk.stop();
    expect(walk.step()).toBeNull();
    expect(sessionStorage.getItem('liwa.walkStep')).toBeNull();
  });
});
