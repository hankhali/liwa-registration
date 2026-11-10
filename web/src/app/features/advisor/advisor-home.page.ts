import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { formatDateTime } from '../../core/academic/academic.models';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { AdvisorOverview, DECIDABLE, QueueItem } from '../../core/planning/advisor.models';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { greetingName } from '../../shared/first-name';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatusChipComponent } from '../../shared/status-chip.component';

/** Advisor home (brief §10): counts, then a "Needs attention" list. */
@Component({
  selector: 'app-advisor-home',
  imports: [MatIconModule, RouterLink, TranslocoPipe, EmptyStateComponent, PageHeaderComponent, StatCardComponent, StatusChipComponent],
  template: `
    <app-page-header [heading]="'advisor.home.welcome' | transloco: { name: name() }" [subtitle]="'advisor.home.subtitle' | transloco" />

    @if (overview.hasValue()) {
      @let o = overview.value();
      <div class="stats">
        <app-stat-card icon="inbox" [label]="'advisor.home.stats.awaiting' | transloco" [value]="o.awaitingReview" link="/advisor/plans" [query]="{ status: 'waiting' }" [attention]="true" />
        <app-stat-card icon="error" [label]="'advisor.home.stats.needsReview' | transloco" [value]="o.needsReview" link="/advisor/plans" [query]="{ status: 'NeedsReview' }" [attention]="true" />
        <app-stat-card icon="check_circle" [label]="'advisor.home.stats.approved' | transloco" [value]="o.approvedThisTerm" link="/advisor/plans" [query]="{ status: 'Approved' }" />
        <app-stat-card icon="rate_review" [label]="'advisor.home.stats.proposed' | transloco" [value]="o.changesProposed" link="/advisor/plans" [query]="{ status: 'ChangesProposed' }" />
      </div>
    }

    <section class="liwa-card block" aria-labelledby="attention-heading">
      <div class="block-head">
        <h2 id="attention-heading">{{ 'advisor.home.needsAttention' | transloco }}</h2>
        <a routerLink="/advisor/plans">{{ 'advisor.home.allPlans' | transloco }}</a>
      </div>
      @if (attention().length === 0) {
        <app-empty-state icon="task_alt" [heading]="'advisor.home.nothingWaiting' | transloco" />
      } @else {
        <ul class="rows">
          @for (q of attention(); track q.planId) {
            <li [class.urgent]="q.urgent">
              <a [routerLink]="['/advisor/plans', q.planId]" class="who">{{ q.studentName }}</a>
              <span class="what"><app-status-chip [status]="q.status" /> {{ 'plan.credits' | transloco: { n: q.credits } }}</span>
              <span class="when">
                @if (q.urgent) {
                  <strong class="flag">{{ 'advisor.queue.urgent' | transloco }}</strong>
                }
                {{ when(q.updatedAt) }}
              </span>
            </li>
          }
        </ul>
      }
    </section>

    <p class="more"><a routerLink="/advisor/students"><mat-icon aria-hidden="true" class="inline-icon">groups</mat-icon> {{ 'advisor.home.allStudents' | transloco }}</a></p>
  `,
  styleUrls: ['../../shared/page.scss', './advisor-home.page.scss'],
})
export class AdvisorHomePage {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);

  protected readonly name = computed(() => greetingName(this.auth.user()?.displayName ?? ''));
  protected readonly overview = resource({ loader: () => firstValueFrom(this.http.get<AdvisorOverview>('/api/advisor/overview')) });
  protected readonly queue = resource({ loader: () => firstValueFrom(this.http.get<QueueItem[]>('/api/advisor/plans')) });
  protected readonly attention = computed(() =>
    (this.queue.value() ?? [])
      .filter((q) => DECIDABLE.includes(q.status))
      .sort((a, b) => Number(b.urgent) - Number(a.urgent) || a.updatedAt.localeCompare(b.updatedAt))
      .slice(0, 8),
  );

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }
}
