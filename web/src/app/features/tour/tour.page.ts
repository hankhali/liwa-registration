import { NgTemplateOutlet } from '@angular/common';
import { Component, afterNextRender, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { TOUR_STOPS, TourService, TourStop } from '../../core/tour/tour.service';

/**
 * The guided tour for a visitor (signed in as admin / admin). Four big buttons, each opens the app as a practice
 * account in one click; the bar at the top of every page brings them back here.
 */
@Component({
  selector: 'app-tour-page',
  imports: [NgTemplateOutlet, MatButtonModule, MatIconModule, MatProgressSpinnerModule, TranslocoPipe],
  template: `
    <header class="tour-head">
      <h1>{{ 'tour.heading' | transloco }}</h1>
      <p class="lead">{{ 'tour.intro' | transloco }}</p>
      <p class="liwa-muted">{{ 'tour.how' | transloco }}</p>
    </header>

    @if (unavailable()) {
      <p class="liwa-card notice" role="alert">{{ 'tour.unavailable' | transloco }}</p>
    }

    <ol class="stops">
      @for (s of stops; track s.key; let i = $index) {
        <li class="liwa-card stop">
          <div class="num" aria-hidden="true">{{ i + 1 }}</div>
          <div class="body">
            <h2><mat-icon aria-hidden="true">{{ s.icon }}</mat-icon> {{ 'tour.stops.' + s.key + '.title' | transloco }}</h2>
            <p>{{ 'tour.stops.' + s.key + '.what' | transloco }}</p>
            <p class="liwa-muted look">{{ 'tour.stops.' + s.key + '.look' | transloco }}</p>
          </div>
          @if (i === 0) {
            <button mat-flat-button class="go" type="button" [disabled]="tour.switching()" (click)="open(s)">
              <ng-container *ngTemplateOutlet="label; context: { $implicit: s }" />
            </button>
          } @else {
            <button mat-stroked-button class="go" type="button" [disabled]="tour.switching()" (click)="open(s)">
              <ng-container *ngTemplateOutlet="label; context: { $implicit: s }" />
            </button>
          }
        </li>
      }
    </ol>

    <ng-template #label let-s>
      @if (busy() === s.key) {
        <mat-spinner diameter="18" [attr.aria-label]="'common.loading' | transloco" />
      } @else {
        {{ 'tour.stops.' + s.key + '.button' | transloco }}
      }
    </ng-template>

    <section class="liwa-card facts" aria-labelledby="facts-heading">
      <h2 id="facts-heading">{{ 'tour.facts.heading' | transloco }}</h2>
      <ul>
        @for (k of facts; track k) {
          <li><mat-icon aria-hidden="true">check</mat-icon> {{ 'tour.facts.' + k | transloco }}</li>
        }
      </ul>
    </section>

    <p class="end">
      <button mat-button type="button" (click)="finish()">
        <mat-icon aria-hidden="true">logout</mat-icon>
        {{ 'tour.end' | transloco }}
      </button>
    </p>
  `,
  styles: `
    :host {
      display: block;
      max-width: 56rem;
    }
    .tour-head h1 {
      margin: 0 0 0.5rem;
      color: var(--liwa-primary);
    }
    .lead {
      font-size: 1.0625rem;
      margin: 0 0 0.5rem;
    }
    .stops {
      list-style: none;
      padding: 0;
      margin: 1.25rem 0;
      display: grid;
      gap: 0.75rem;
    }
    .stop {
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: 1rem;
      align-items: center;
    }
    .num {
      width: 2.25rem;
      height: 2.25rem;
      border-radius: 50%;
      display: grid;
      place-items: center;
      font-weight: 700;
      background: var(--liwa-primary);
      color: #fff;
    }
    .body h2 {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0 0 0.25rem;
      font-size: 1.125rem;
    }
    .body p {
      margin: 0 0 0.25rem;
    }
    .go {
      min-height: 48px;
      min-width: 11rem;
    }
    .look {
      font-size: 0.875rem;
    }
    .facts h2 {
      margin: 0 0 0.5rem;
      font-size: 1.0625rem;
    }
    .facts ul {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.375rem;
    }
    .facts li {
      display: flex;
      gap: 0.5rem;
      align-items: flex-start;
    }
    .facts mat-icon {
      color: var(--liwa-success);
      flex: none;
    }
    .notice {
      color: var(--liwa-danger);
    }
    .end {
      margin-top: 1rem;
    }
    @media (max-width: 600px) {
      .stop {
        grid-template-columns: auto 1fr;
      }
      .stop button {
        grid-column: 1 / -1;
      }
    }
  `,
})
export class TourPage {
  protected readonly tour = inject(TourService);
  private readonly auth = inject(AuthService);
  protected readonly stops = TOUR_STOPS.filter((s) => s.key !== 'admin').concat(TOUR_STOPS.filter((s) => s.key === 'admin'));
  protected readonly facts = ['catalog', 'data', 'rules', 'languages', 'phone'];
  protected readonly busy = signal<string | null>(null);
  protected readonly unavailable = signal(false);

  constructor() {
    this.tour.start();
    afterNextRender(() => void this.tour.load().then((d) => this.unavailable.set(d === null)));
  }

  protected async open(stop: TourStop): Promise<void> {
    this.busy.set(stop.key);
    const ok = await this.tour.visit(stop);
    this.busy.set(null);
    if (!ok) this.unavailable.set(true);
  }

  protected finish(): void {
    this.tour.end();
    this.auth.signOut('user');
  }
}
