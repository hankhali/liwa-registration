import { Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { greetingName } from '../../shared/first-name';

/** Student landing page. Plain language only: no system or technical information for students. */
@Component({
  selector: 'app-student-home',
  imports: [MatIconModule, TranslocoPipe],
  template: `
    <header class="page-header">
      <h1>{{ 'student.home.welcome' | transloco: { name: name() } }}</h1>
      <p class="liwa-muted">{{ 'student.home.subtitle' | transloco }}</p>
    </header>

    <section class="liwa-card plan-card" aria-labelledby="plan-heading">
      <h2 id="plan-heading" class="card-title">
        <mat-icon aria-hidden="true">event_note</mat-icon>
        {{ 'student.home.planHeading' | transloco }}
      </h2>
      <p>{{ 'student.home.planComingSoon' | transloco }}</p>

      <ol class="steps">
        @for (step of steps; track step.key; let i = $index) {
          <li>
            <span class="step-number" aria-hidden="true">{{ i + 1 }}</span>
            <span class="step-text">
              <strong>{{ 'student.home.steps.' + step.key + '.title' | transloco }}</strong>
              <span class="liwa-muted">{{ 'student.home.steps.' + step.key + '.body' | transloco }}</span>
            </span>
          </li>
        }
      </ol>
    </section>
  `,
  styleUrls: ['../../shared/page.scss', './student-home.page.scss'],
})
export class StudentHomePage {
  private readonly auth = inject(AuthService);
  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
  protected readonly steps = [{ key: 'availability' }, { key: 'review' }, { key: 'send' }];
}
