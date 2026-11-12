import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/** What the API returns in Development with demo data on (404 everywhere else). */
export interface DemoAccounts {
  password: string;
  adminPassword: string;
  accounts: { username: string; role: string; description: string }[];
}

/** One stop on the guided tour: which practice account it signs in as and where it opens. */
export interface TourStop {
  key: 'student' | 'studentPlan' | 'advisor' | 'admin';
  icon: string;
  username: string;
  link: string;
}

export const TOUR_STOPS: TourStop[] = [
  { key: 'student', icon: 'school', username: 'student.hanieh', link: '/student/planner' },
  { key: 'studentPlan', icon: 'fact_check', username: 'student.transfer', link: '/student/plan' },
  { key: 'advisor', icon: 'supervisor_account', username: 'advisor.it', link: '/advisor/plans' },
  { key: 'admin', icon: 'admin_panel_settings', username: 'admin', link: '/admin' },
];

const TOURING_KEY = 'liwa.touring';

/**
 * The guided demo tour for visitors (e.g. a professor reviewing the project): sign in once as admin / admin,
 * then switch between practice accounts with one click and come back to the tour from a bar at the top.
 * Only works when the API exposes the practice accounts (Development with demo data on).
 */
@Injectable({ providedIn: 'root' })
export class TourService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly demo = signal<DemoAccounts | null>(null);
  readonly touring = signal(this.read());
  readonly switching = signal(false);

  /** Loads the practice accounts once; null when the server has no demo data. */
  async load(): Promise<DemoAccounts | null> {
    if (this.demo()) return this.demo();
    try {
      this.demo.set(await firstValueFrom(this.http.get<DemoAccounts>('/api/auth/demo-accounts')));
    } catch {
      this.demo.set(null);
    }
    return this.demo();
  }

  /** True when this sign-in should open the tour instead of the normal home page. */
  async isTourSignIn(username: string): Promise<boolean> {
    if (username.trim().toLowerCase() !== 'admin') return false;
    return (await this.load()) !== null;
  }

  start(): void {
    this.setTouring(true);
  }

  /** Signs in as a practice account and opens its screen. */
  async visit(stop: TourStop): Promise<boolean> {
    const demo = await this.load();
    if (!demo || this.switching()) return false;
    this.switching.set(true);
    try {
      const password = stop.username === 'admin' ? demo.adminPassword : demo.password;
      const result = await this.auth.signIn(stop.username, password);
      if (!result.ok) return false;
      this.setTouring(true);
      return this.router.navigateByUrl(stop.link);
    } finally {
      this.switching.set(false);
    }
  }

  /** Signs in as a practice account without changing page (the walkthrough opens the page itself). */
  async switchTo(username: string): Promise<boolean> {
    const demo = await this.load();
    if (!demo) return false;
    this.switching.set(true);
    try {
      const result = await this.auth.signIn(username, username === 'admin' ? demo.adminPassword : demo.password);
      if (result.ok) this.setTouring(true);
      return result.ok;
    } finally {
      this.switching.set(false);
    }
  }

  /** Back to the tour page as the admin. */
  async back(): Promise<boolean> {
    const demo = await this.load();
    if (!demo || this.switching()) return false;
    this.switching.set(true);
    try {
      const result = await this.auth.signIn('admin', demo.adminPassword);
      if (!result.ok) return false;
      return this.router.navigateByUrl('/tour');
    } finally {
      this.switching.set(false);
    }
  }

  end(): void {
    this.setTouring(false);
  }

  private setTouring(on: boolean): void {
    this.touring.set(on);
    try {
      if (on) sessionStorage.setItem(TOURING_KEY, '1');
      else sessionStorage.removeItem(TOURING_KEY);
    } catch {
      /* the bar just won't survive a reload */
    }
  }

  private read(): boolean {
    try {
      return sessionStorage.getItem(TOURING_KEY) === '1';
    } catch {
      return false;
    }
  }
}
