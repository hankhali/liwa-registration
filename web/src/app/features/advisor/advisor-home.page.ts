import { Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { greetingName } from '../../shared/first-name';

@Component({
  selector: 'app-advisor-home',
  imports: [MatIconModule, TranslocoPipe],
  template: `
    <header class="page-header">
      <h1>{{ 'advisor.home.welcome' | transloco: { name: name() } }}</h1>
      <p class="liwa-muted">{{ 'advisor.home.subtitle' | transloco }}</p>
    </header>

    <div class="grid">
      <section class="liwa-card" aria-labelledby="attention-heading">
        <h2 id="attention-heading" class="card-title">
          <mat-icon aria-hidden="true">priority_high</mat-icon>
          {{ 'advisor.home.needsAttention' | transloco }}
        </h2>
        <p class="empty">
          <mat-icon aria-hidden="true">check_circle</mat-icon>
          {{ 'advisor.home.nothingWaiting' | transloco }}
        </p>
      </section>

      <section class="liwa-card" aria-labelledby="advisees-heading">
        <h2 id="advisees-heading" class="card-title">
          <mat-icon aria-hidden="true">groups</mat-icon>
          {{ 'advisor.home.advisees' | transloco }}
        </h2>
        <p class="liwa-muted">{{ 'advisor.home.adviseesComingSoon' | transloco }}</p>
      </section>
    </div>
  `,
  styleUrl: '../../shared/page.scss',
})
export class AdvisorHomePage {
  private readonly auth = inject(AuthService);
  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
}
