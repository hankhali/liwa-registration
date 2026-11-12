import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TOUR_STOPS, TourService } from './tour.service';

const demo = { password: 'Liwa#2027', adminPassword: 'admin', accounts: [] };
const signedIn = (username: string, role: string) => ({
  accessToken: 't-' + username,
  expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  user: { id: username, username, displayName: username, role, universityId: null },
});

function setup() {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  return { tour: TestBed.inject(TourService), http: TestBed.inject(HttpTestingController), navigate };
}

const tick = () => new Promise((r) => setTimeout(r));

describe('TourService (guided demo tour)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    sessionStorage.clear();
    localStorage.clear();
  });
  afterEach(() => sessionStorage.clear());

  it('opens a stop by signing in as its practice account with the shared password', async () => {
    const { tour, http, navigate } = setup();
    const student = TOUR_STOPS.find((s) => s.key === 'student')!;
    const done = tour.visit(student);
    http.expectOne('/api/auth/demo-accounts').flush(demo);
    await tick();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.body).toEqual({ username: 'student.hanieh', password: 'Liwa#2027' });
    login.flush(signedIn('student.hanieh', 'Student'));
    expect(await done).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/student/planner');
    expect(tour.touring()).toBe(true);
    expect(sessionStorage.getItem('liwa.touring')).toBe('1');
  });

  it('goes back to the tour as admin with the admin password', async () => {
    const { tour, http, navigate } = setup();
    const done = tour.back();
    http.expectOne('/api/auth/demo-accounts').flush(demo);
    await tick();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.body).toEqual({ username: 'admin', password: 'admin' });
    login.flush(signedIn('admin', 'Admin'));
    expect(await done).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/tour');
  });

  it('only treats an admin sign-in as a tour on a demo server', async () => {
    const { tour, http } = setup();
    expect(await tour.isTourSignIn('student.new')).toBe(false);
    const none = tour.isTourSignIn('admin');
    http.expectOne('/api/auth/demo-accounts').flush('', { status: 404, statusText: 'Not Found' });
    expect(await none).toBe(false);
    expect(tour.demo()).toBeNull();
  });

  it('ends the tour', () => {
    const { tour } = setup();
    tour.start();
    tour.end();
    expect(tour.touring()).toBe(false);
    expect(sessionStorage.getItem('liwa.touring')).toBeNull();
  });
});
