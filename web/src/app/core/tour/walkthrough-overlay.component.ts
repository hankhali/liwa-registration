import { Component, DestroyRef, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../auth/auth.service';
import { TourService } from './tour.service';
import { WALK_PARTS, WalkthroughService } from './walkthrough';

interface Box {
  top: number;
  left: number;
  width: number;
  height: number;
}

const PAD = 8;
const GAP = 14;
const MARGIN = 16;

/**
 * The guided walkthrough on top of the app: the page dims, a spotlight glides to the thing being explained, and a card
 * next to it says what it is and what to click. Clicks still reach the page (the dimming ignores the pointer).
 */
@Component({
  selector: 'app-walkthrough-overlay',
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, TranslocoPipe],
  template: `
    @if (walk.step(); as s) {
      <div class="walk" [class.centred]="!spot()">
        <div
          class="spot"
          [class.pulse]="s.kind === 'click' && !walk.moving()"
          aria-hidden="true"
          [style.top.px]="shown().top"
          [style.left.px]="shown().left"
          [style.width.px]="shown().width"
          [style.height.px]="shown().height"
        ></div>

        @for (k of [s.id]; track k) {
          <section
            #card
            class="card"
            [class.scene]="s.kind === 'scene' || s.kind === 'end'"
            role="dialog"
            aria-modal="false"
            aria-labelledby="walk-title"
            aria-describedby="walk-body"
            [style.top.px]="cardPos().top"
            [style.left.px]="cardPos().left"
          >
            @if (walk.position(); as p) {
              <p class="where">
                <span class="part">{{ 'tour.walk.partOf' | transloco: { part: p.part, parts: parts } }} · {{ 'tour.walk.parts.' + p.part | transloco }}</span>
                @if (s.kind !== 'scene') {
                  <span class="liwa-muted">{{ 'tour.walk.stepOf' | transloco: { n: p.n, of: p.of } }}</span>
                }
              </p>
              <div class="bar" aria-hidden="true"><span [style.width.%]="progress()"></span></div>
            }

            <h2 id="walk-title">{{ 'tour.walk.steps.' + s.id + '.title' | transloco }}</h2>
            <p id="walk-body">{{ 'tour.walk.steps.' + s.id + '.body' | transloco }}</p>

            @if (s.kind === 'click' && !walk.moving()) {
              <p class="hint"><mat-icon aria-hidden="true">touch_app</mat-icon> {{ 'tour.walk.clickHint' | transloco }}</p>
            }
            @if (walk.moving()) {
              <p class="opening" role="status"><mat-spinner diameter="18" aria-hidden="true" /> {{ 'tour.walk.opening' | transloco }}</p>
            }

            @if (s.kind === 'end') {
              <div class="buttons">
                <button mat-flat-button type="button" (click)="explore()">{{ 'tour.walk.explore' | transloco }}</button>
                <button mat-stroked-button type="button" (click)="walk.start(0)">{{ 'tour.walk.again' | transloco }}</button>
                <button mat-button type="button" (click)="signOut()">{{ 'tour.walk.signOut' | transloco }}</button>
              </div>
            } @else {
              <div class="buttons">
                @if (walk.index()! > 0) {
                  <button mat-button type="button" (click)="walk.back()" [disabled]="walk.moving()">
                    <mat-icon aria-hidden="true" class="flip-rtl">arrow_back</mat-icon> {{ 'tour.walk.back' | transloco }}
                  </button>
                }
                <button mat-button type="button" class="exit" (click)="explore()">{{ 'tour.walk.exit' | transloco }}</button>
                <button mat-flat-button type="button" class="next" (click)="walk.next()" [disabled]="walk.moving()">
                  {{ (s.kind === 'scene' ? (walk.index() === 0 ? 'tour.walk.start' : 'tour.walk.go') : 'tour.walk.next') | transloco }}
                  <mat-icon aria-hidden="true" iconPositionEnd class="flip-rtl">arrow_forward</mat-icon>
                </button>
              </div>
              @if (walk.index() === 0) {
                <p class="keys liwa-muted">{{ 'tour.walk.keys' | transloco }}</p>
              }
            }
          </section>
        }
      </div>
    }
  `,
  styles: `
    .walk {
      position: fixed;
      inset: 0;
      z-index: 900;
      pointer-events: none;
    }
    .spot {
      position: fixed;
      border-radius: 12px;
      box-shadow: 0 0 0 200vmax rgba(22, 22, 44, 0.58);
      outline: 3px solid var(--liwa-gold);
      outline-offset: 0;
      transition:
        top 0.5s cubic-bezier(0.4, 0, 0.2, 1),
        left 0.5s cubic-bezier(0.4, 0, 0.2, 1),
        width 0.5s cubic-bezier(0.4, 0, 0.2, 1),
        height 0.5s cubic-bezier(0.4, 0, 0.2, 1),
        outline-color 0.3s;
    }
    .centred .spot {
      outline-color: transparent;
    }
    .spot.pulse {
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse {
      0%,
      100% {
        outline-offset: 0;
        outline-width: 3px;
      }
      50% {
        outline-offset: 6px;
        outline-width: 5px;
      }
    }
    .card {
      position: fixed;
      pointer-events: auto;
      width: min(24rem, calc(100vw - 2rem));
      box-sizing: border-box;
      padding: 1rem 1.125rem 0.875rem;
      border-radius: 14px;
      background: var(--liwa-surface);
      color: var(--liwa-text);
      border-top: 4px solid var(--liwa-gold);
      box-shadow: 0 18px 48px rgba(0, 0, 0, 0.3);
      transition:
        top 0.5s cubic-bezier(0.4, 0, 0.2, 1),
        left 0.5s cubic-bezier(0.4, 0, 0.2, 1);
      animation: card-in 0.35s ease-out;
    }
    .card.scene {
      width: min(30rem, calc(100vw - 2rem));
      padding: 1.5rem 1.5rem 1.125rem;
    }
    @keyframes card-in {
      from {
        opacity: 0;
        transform: translateY(10px) scale(0.98);
      }
      to {
        opacity: 1;
        transform: none;
      }
    }
    .where {
      display: flex;
      justify-content: space-between;
      gap: 0.5rem;
      margin: 0 0 0.375rem;
      font-size: 0.8125rem;
    }
    .part {
      font-weight: 700;
      color: var(--liwa-primary);
    }
    .bar {
      height: 4px;
      border-radius: 2px;
      background: var(--liwa-border);
      margin-bottom: 0.75rem;
      overflow: hidden;
      span {
        display: block;
        height: 100%;
        background: var(--liwa-gold);
        transition: width 0.5s ease;
      }
    }
    h2 {
      margin: 0 0 0.375rem;
      font-size: 1.125rem;
      color: var(--liwa-primary);
    }
    .scene h2 {
      font-size: 1.375rem;
    }
    p {
      margin: 0 0 0.625rem;
      line-height: 1.5;
    }
    .hint,
    .opening {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-weight: 600;
    }
    .hint mat-icon {
      color: var(--liwa-gold);
    }
    .buttons {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.25rem 0.5rem;
      margin-top: 0.25rem;
    }
    .next {
      margin-inline-start: auto;
      min-height: 44px;
    }
    .exit {
      color: var(--liwa-text-muted);
    }
    .keys {
      margin: 0.5rem 0 0;
      font-size: 0.8125rem;
    }
    @media (max-width: 600px) {
      .card,
      .card.scene {
        left: 1rem !important;
        top: auto !important;
        bottom: 1rem;
        width: calc(100vw - 2rem);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .spot,
      .card {
        transition: none;
        animation: none;
      }
    }
  `,
  host: {
    '(document:keydown)': 'onKey($event)',
    '(window:resize)': 'measure()',
    '(document:scroll)': 'measure()',
  },
})
export class WalkthroughOverlayComponent {
  protected readonly walk = inject(WalkthroughService);
  private readonly tour = inject(TourService);
  private readonly auth = inject(AuthService);
  private readonly card = viewChild<ElementRef<HTMLElement>>('card');
  protected readonly parts = WALK_PARTS;

  /** The highlighted thing on screen (padded), or null for a centred card. */
  protected readonly spot = signal<Box | null>(null);
  private readonly viewport = signal({ w: window.innerWidth, h: window.innerHeight });
  private readonly cardSize = signal({ w: 384, h: 220 });
  private target: Element | null = null;
  private readonly onTargetClick = () => void this.walk.clicked();

  /** With nothing to highlight, the spotlight shrinks to the middle (the whole page dims). */
  protected readonly shown = computed<Box>(() => {
    const vp = this.viewport();
    return this.spot() ?? { top: vp.h / 2, left: vp.w / 2, width: 0, height: 0 };
  });

  protected readonly progress = computed(() => {
    const p = this.walk.position();
    return p ? Math.round(((p.part - 1) / p.parts) * 100 + (p.n / p.of) * (100 / p.parts)) : 0;
  });

  /** Below the spotlight if it fits, else above, else beside it, else at the bottom of the screen. */
  protected readonly cardPos = computed(() => {
    const vp = this.viewport();
    const { w, h } = this.cardSize();
    const s = this.spot();
    if (!s) return { top: Math.max(MARGIN, (vp.h - h) / 2), left: Math.max(MARGIN, (vp.w - w) / 2) };
    const clampX = (x: number) => Math.min(Math.max(MARGIN, x), vp.w - w - MARGIN);
    const clampY = (y: number) => Math.min(Math.max(MARGIN, y), vp.h - h - MARGIN);
    if (s.top + s.height + GAP + h <= vp.h - MARGIN) return { top: s.top + s.height + GAP, left: clampX(s.left) };
    if (s.top - GAP - h >= MARGIN) return { top: s.top - GAP - h, left: clampX(s.left) };
    if (s.left + s.width + GAP + w <= vp.w - MARGIN) return { top: clampY(s.top), left: s.left + s.width + GAP };
    if (s.left - GAP - w >= MARGIN) return { top: clampY(s.top), left: s.left - GAP - w };
    return { top: vp.h - h - MARGIN, left: vp.w - w - MARGIN };
  });

  constructor() {
    // Each time a step has loaded: find its target, scroll it into view, then follow it.
    effect(() => {
      this.walk.arrived();
      const s = this.walk.step();
      const moving = this.walk.moving();
      this.release();
      if (!s || moving || !s.target) {
        untracked(() => this.spot.set(null));
        return;
      }
      const el = this.walk.find(s);
      if (!el) {
        untracked(() => this.spot.set(null));
        return;
      }
      this.target = el;
      if (s.kind === 'click') el.addEventListener('click', this.onTargetClick, { once: true });
      const tall = el.getBoundingClientRect().height > window.innerHeight * 0.6;
      el.scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: tall ? 'start' : 'center' });
      untracked(() => this.measure());
    });

    // Layout keeps moving while data loads and while smooth-scrolling: re-measure a few times a second.
    const timer = setInterval(() => this.measure(), 150);
    // Scrolling inside the main area doesn't reach document:scroll.
    const onScroll = () => this.measure();
    document.addEventListener('scroll', onScroll, true);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(timer);
      document.removeEventListener('scroll', onScroll, true);
      this.release();
    });
  }

  protected measure(): void {
    this.viewport.set({ w: window.innerWidth, h: window.innerHeight });
    const card = this.card()?.nativeElement;
    if (card) {
      const r = card.getBoundingClientRect();
      if (r.width && (Math.abs(r.width - this.cardSize().w) > 1 || Math.abs(r.height - this.cardSize().h) > 1)) this.cardSize.set({ w: r.width, h: r.height });
    }
    if (!this.target || !this.target.isConnected) return;
    const r = this.target.getBoundingClientRect();
    const vh = window.innerHeight;
    // Keep the spotlight on screen for things taller than the window.
    const top = Math.max(r.top - PAD, 72);
    const bottom = Math.min(r.bottom + PAD, vh - 8);
    // ...and wider ones (a wide table in a scrolling box).
    const left = Math.max(r.left - PAD, 8);
    const right = Math.min(r.right + PAD, window.innerWidth - 8);
    const next = { top, left, width: Math.max(right - left, 24), height: Math.max(bottom - top, 24) };
    const cur = this.spot();
    if (!cur || Math.abs(cur.top - next.top) > 0.5 || Math.abs(cur.left - next.left) > 0.5 || Math.abs(cur.width - next.width) > 0.5 || Math.abs(cur.height - next.height) > 0.5) this.spot.set(next);
  }

  protected onKey(e: KeyboardEvent): void {
    if (this.walk.index() === null) return;
    const t = e.target as HTMLElement | null;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return;
    const rtl = document.documentElement.dir === 'rtl';
    if (e.key === (rtl ? 'ArrowLeft' : 'ArrowRight')) {
      e.preventDefault();
      void this.walk.next();
    } else if (e.key === (rtl ? 'ArrowRight' : 'ArrowLeft')) {
      e.preventDefault();
      void this.walk.back();
    } else if (e.key === 'Escape') this.explore();
  }

  /** Leave the walkthrough but stay signed in; the bar at the top leads back to the tour. */
  protected explore(): void {
    this.walk.stop();
  }

  protected signOut(): void {
    this.walk.stop();
    this.tour.end();
    this.auth.signOut('user');
  }

  private release(): void {
    this.target?.removeEventListener('click', this.onTargetClick);
    this.target = null;
  }
}
