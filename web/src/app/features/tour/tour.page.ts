import { Component, afterNextRender, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { TourService } from '../../core/tour/tour.service';
import { WALK_STEPS, WalkthroughService } from '../../core/tour/walkthrough';

interface Part {
  part: number;
  key: 'student' | 'advisor' | 'studentPlan' | 'admin';
  icon: string;
}

/**
 * The guided tour's start page (signed in as admin / admin on a demo server). One button starts the walkthrough,
 * which goes through every step in order; each part can also be started on its own.
 */
@Component({
  selector: 'app-tour-page',
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  template: `
    <header class="tour-head">
      <h1>{{ 'tour.heading' | transloco }}</h1>
      <p class="lead">{{ 'tour.intro' | transloco }}</p>
      <p>
        <button mat-flat-button type="button" class="start" (click)="startAll()" [disabled]="unavailable()">
          <mat-icon aria-hidden="true">play_arrow</mat-icon>
          {{ 'tour.startWalk' | transloco }}
        </button>
      </p>
      <p class="liwa-muted">{{ 'tour.how' | transloco }}</p>
    </header>

    @if (unavailable()) {
      <p class="liwa-card notice" role="alert">{{ 'tour.unavailable' | transloco }}</p>
    }

    <h2 class="parts-heading">{{ 'tour.partsHeading' | transloco }}</h2>
    <ol class="stops">
      @for (p of parts; track p.key) {
        <li class="liwa-card stop">
          <div class="num" aria-hidden="true">{{ p.part }}</div>
          <div class="body">
            <h3><mat-icon aria-hidden="true">{{ p.icon }}</mat-icon> {{ 'tour.stops.' + p.key + '.title' | transloco }}</h3>
            <p>{{ 'tour.stops.' + p.key + '.what' | transloco }}</p>
          </div>
          <button mat-stroked-button class="go" type="button" [disabled]="unavailable()" (click)="startPart(p.part)">
            {{ 'tour.startPart' | transloco }}
          </button>
        </li>
      }
    </ol>

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
      margin: 0 0 1rem;
    }
    .start {
      min-height: 52px;
      font-size: 1.0625rem;
      padding-inline: 1.5rem;
    }
    .parts-heading {
      font-size: 1.0625rem;
      margin: 1.5rem 0 0.5rem;
    }
    .stops {
      list-style: none;
      padding: 0;
      margin: 0 0 1.25rem;
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
    .body h3 {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0 0 0.25rem;
      font-size: 1.0625rem;
    }
    .body p {
      margin: 0;
    }
    .go {
      min-height: 44px;
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
  private readonly tour = inject(TourService);
  private readonly walk = inject(WalkthroughService);
  private readonly auth = inject(AuthService);
  protected readonly parts: Part[] = [
    { part: 1, key: 'student', icon: 'school' },
    { part: 2, key: 'advisor', icon: 'supervisor_account' },
    { part: 3, key: 'studentPlan', icon: 'fact_check' },
    { part: 4, key: 'admin', icon: 'admin_panel_settings' },
  ];
  protected readonly facts = ['catalog', 'data', 'rules', 'languages', 'phone'];
  protected readonly unavailable = signal(false);

  constructor() {
    this.tour.start();
    afterNextRender(() => void this.tour.load().then((d) => this.unavailable.set(d === null)));
  }

  protected startAll(): void {
    void this.walk.start(0);
  }

  /** Each part starts at its introduction card. */
  protected startPart(part: number): void {
    void this.walk.start(WALK_STEPS.findIndex((s) => s.part === part));
  }

  protected finish(): void {
    this.walk.stop();
    this.tour.end();
    this.auth.signOut('user');
  }
}
