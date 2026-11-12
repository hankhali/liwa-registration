import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { TourService } from './tour.service';

/**
 * One step of the guided walkthrough. A "scene" is a centred card that introduces a part of the story (and signs in
 * as that part's practice account behind it); a "look" step puts a spotlight on something; a "click" step asks the
 * visitor to click the highlighted thing (Next clicks it for them).
 */
export interface WalkStep {
  id: string;
  kind: 'scene' | 'look' | 'click' | 'end';
  /** Practice account this step is seen as. */
  account: string;
  /** Page to open first; omitted = stay where the previous step left us (after a click). */
  route?: string;
  /** CSS selectors for the thing to highlight; the first one found wins. */
  target?: string[];
  /** Part of the story (1-4) for the progress line. */
  part: number;
}

export const WALK_PARTS = 4;

export const WALK_STEPS: WalkStep[] = [
  { id: 'welcome', kind: 'scene', account: 'admin', route: '/tour', part: 0 },

  // Part 1: the student
  { id: 'studentIntro', kind: 'scene', account: 'student.hanieh', route: '/student', part: 1 },
  { id: 'home', kind: 'look', account: 'student.hanieh', route: '/student', target: ['.answer'], part: 1 },
  { id: 'freeTime', kind: 'look', account: 'student.hanieh', route: '/student/availability', target: ['.presets', 'app-timetable-grid'], part: 1 },
  { id: 'classes', kind: 'look', account: 'student.hanieh', route: '/student/planner', target: ['.courses .course', '.courses'], part: 1 },
  { id: 'howMany', kind: 'look', account: 'student.hanieh', route: '/student/planner', target: ['.load', '.credits-big'], part: 1 },
  { id: 'week', kind: 'look', account: 'student.hanieh', route: '/student/planner', target: ['.plan app-timetable-grid'], part: 1 },
  { id: 'graduate', kind: 'look', account: 'student.hanieh', route: '/student/planner', target: ['.plan .graduation'], part: 1 },
  { id: 'send', kind: 'look', account: 'student.hanieh', route: '/student/planner', target: ['button.send', 'app-page-header .actions'], part: 1 },
  { id: 'graduation', kind: 'look', account: 'student.hanieh', route: '/student/graduation', target: ['.headline'], part: 1 },

  // Part 2: the advisor
  { id: 'advisorIntro', kind: 'scene', account: 'advisor.it', route: '/advisor/plans', part: 2 },
  { id: 'queue', kind: 'look', account: 'advisor.it', route: '/advisor/plans', target: ['table.queue', '.empty'], part: 2 },
  { id: 'openPlan', kind: 'click', account: 'advisor.it', route: '/advisor/plans', target: ['table.queue tbody tr:first-child a'], part: 2 },
  { id: 'reviewStudent', kind: 'look', account: 'advisor.it', target: ['.col-left'], part: 2 },
  { id: 'reviewPlan', kind: 'look', account: 'advisor.it', target: ['.col-centre'], part: 2 },
  { id: 'reviewGrad', kind: 'look', account: 'advisor.it', target: ['.col-right'], part: 2 },
  { id: 'decide', kind: 'look', account: 'advisor.it', target: ['.decide'], part: 2 },

  // Part 3: the student gets the advisor's suggestion
  { id: 'feedbackIntro', kind: 'scene', account: 'student.transfer', route: '/student/plan', part: 3 },
  { id: 'compare', kind: 'look', account: 'student.transfer', route: '/student/plan', target: ['.compare', 'app-page-header'], part: 3 },
  { id: 'reply', kind: 'look', account: 'student.transfer', route: '/student/plan', target: ['.compare ~ .actions', 'app-page-header .actions'], part: 3 },

  // Part 4: the registrar
  { id: 'registrarIntro', kind: 'scene', account: 'admin', route: '/admin/catalog', part: 4 },
  { id: 'catalog', kind: 'look', account: 'admin', route: '/admin/catalog', target: ['table'], part: 4 },
  { id: 'rules', kind: 'look', account: 'admin', route: '/admin/settings', target: ['table.rules', 'table'], part: 4 },

  { id: 'end', kind: 'end', account: 'admin', route: '/tour', part: 0 },
];

const WALK_KEY = 'liwa.walkStep';

/** Runs the guided walkthrough: which step is showing, switching accounts and pages between steps. */
@Injectable({ providedIn: 'root' })
export class WalkthroughService {
  private readonly tour = inject(TourService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly steps = WALK_STEPS;
  /** Index of the step showing, or null when the walkthrough is off. */
  readonly index = signal<number | null>(this.read());
  readonly step = computed(() => {
    const i = this.index();
    return i === null ? null : WALK_STEPS[i];
  });
  /** True while switching account or page (the card shows "Opening…"). */
  readonly moving = signal(false);
  /** How long to wait for the thing to highlight before showing the card anyway. */
  waitMs = 8000;
  /** Bumped each time a step finished loading, so the overlay re-measures. */
  readonly arrived = signal(0);

  /** Step number among the steps of the same part ("2 of 8"). */
  readonly position = computed(() => {
    const s = this.step();
    if (!s || s.part === 0) return null;
    const inPart = WALK_STEPS.filter((x) => x.part === s.part && x.kind !== 'scene');
    const n = inPart.indexOf(s);
    return { part: s.part, parts: WALK_PARTS, n: n + 1, of: inPart.length };
  });

  async start(at = 0): Promise<void> {
    this.tour.start();
    await this.go(at);
  }

  async next(): Promise<void> {
    const i = this.index();
    if (i === null || this.moving()) return;
    const s = WALK_STEPS[i];
    if (s.kind === 'click') {
      // Do the click for the visitor, then wait for the page it opens.
      const el = this.find(s);
      if (el instanceof HTMLElement) el.click();
      // The overlay's click listener usually moves on already.
      if (!this.moving() && this.index() === i) await this.go(i + 1, false);
      return;
    }
    if (i + 1 < WALK_STEPS.length) await this.go(i + 1);
  }

  async back(): Promise<void> {
    const i = this.index();
    if (i === null || i === 0 || this.moving()) return;
    // Same page (both opened by an earlier click): just move the spotlight back.
    if (!WALK_STEPS[i].route && !WALK_STEPS[i - 1].route) {
      await this.go(i - 1, false);
      return;
    }
    // Steps without a route depend on a click: go back to the step that made it.
    let j = i - 1;
    while (j > 0 && !WALK_STEPS[j].route) j--;
    await this.go(j);
  }

  /** The visitor clicked the highlighted thing themselves on a "click" step. */
  async clicked(): Promise<void> {
    const i = this.index();
    if (i === null || WALK_STEPS[i].kind !== 'click' || this.moving()) return;
    await this.go(i + 1, false);
  }

  stop(): void {
    this.index.set(null);
    this.write(null);
  }

  find(s: WalkStep): Element | null {
    for (const sel of s.target ?? []) {
      const el = document.querySelector(sel);
      if (el && el.getBoundingClientRect().height > 0) return el;
    }
    return null;
  }

  private async go(i: number, navigate = true): Promise<void> {
    const s = WALK_STEPS[i];
    this.moving.set(true);
    this.index.set(i);
    this.write(i);
    try {
      if (this.auth.user()?.username !== s.account) {
        const ok = await this.tour.switchTo(s.account);
        if (!ok) {
          this.stop();
          return;
        }
      }
      if (navigate && s.route && this.router.url.split('?')[0] !== s.route) await this.router.navigateByUrl(s.route);
      if (s.target) await this.waitFor(s);
    } finally {
      this.moving.set(false);
      this.arrived.update((n) => n + 1);
    }
  }

  /** Pages load their data after opening: wait (up to 8 s) for the thing to highlight. */
  private waitFor(s: WalkStep): Promise<void> {
    return new Promise((resolve) => {
      const started = Date.now();
      const check = () => {
        if (this.find(s) || Date.now() - started >= this.waitMs) resolve();
        else setTimeout(check, 120);
      };
      check();
    });
  }

  private read(): number | null {
    try {
      const v = sessionStorage.getItem(WALK_KEY);
      const n = v === null ? NaN : Number(v);
      return Number.isInteger(n) && n >= 0 && n < WALK_STEPS.length ? n : null;
    } catch {
      return null;
    }
  }

  private write(i: number | null): void {
    try {
      if (i === null) sessionStorage.removeItem(WALK_KEY);
      else sessionStorage.setItem(WALK_KEY, String(i));
    } catch {
      /* the walkthrough won't survive a reload */
    }
  }
}
