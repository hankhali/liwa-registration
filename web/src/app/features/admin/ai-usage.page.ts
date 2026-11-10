import { HttpClient } from '@angular/common/http';
import { Component, inject, resource } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { StatCardComponent } from '../../shared/stat-card.component';

export interface AiUsage {
  enabled: boolean;
  provider: string;
  model: string;
  days: { date: string; requests: number; inputTokens: number; outputTokens: number; estimatedCostUsd: number; fallbacks: number; discards: number; cacheHits: number }[];
  recent: { at: string; kind: 'Fallback' | 'Discard' | 'RateLimited'; reason: string }[];
}

/** AI usage (brief §11): requests per day, tokens, estimated cost, recent fallbacks and discarded replies. */
@Component({
  selector: 'app-ai-usage-page',
  imports: [TranslocoPipe, EmptyStateComponent, PageHeaderComponent, StatCardComponent],
  template: `
    <app-page-header [heading]="'admin.aiUsage.heading' | transloco" [subtitle]="'admin.aiUsage.subtitle' | transloco" />
    @if (usage.hasValue()) {
      @let u = usage.value();
      <p class="liwa-muted">
        {{ (u.enabled ? 'admin.aiUsage.on' : 'admin.aiUsage.off') | transloco: { provider: u.provider, model: u.model } }}
      </p>
      <div class="stats">
        <app-stat-card icon="chat" [label]="'admin.aiUsage.requests' | transloco" [value]="total('requests')" />
        <app-stat-card icon="data_usage" [label]="'admin.aiUsage.tokens' | transloco" [value]="total('inputTokens') + total('outputTokens')" />
        <app-stat-card icon="undo" [label]="'admin.aiUsage.fallbacks' | transloco" [value]="total('fallbacks')" [attention]="true" />
        <app-stat-card icon="block" [label]="'admin.aiUsage.discards' | transloco" [value]="total('discards')" [attention]="true" />
      </div>
      @if (u.days.length === 0) {
        <app-empty-state icon="insights" [heading]="'admin.aiUsage.none' | transloco" />
      } @else {
        <section class="liwa-card">
          <table class="liwa-table">
            <thead>
              <tr>
                <th scope="col">{{ 'admin.aiUsage.date' | transloco }}</th>
                <th scope="col">{{ 'admin.aiUsage.requests' | transloco }}</th>
                <th scope="col">{{ 'admin.aiUsage.in' | transloco }}</th>
                <th scope="col">{{ 'admin.aiUsage.out' | transloco }}</th>
                <th scope="col">{{ 'admin.aiUsage.cost' | transloco }}</th>
                <th scope="col">{{ 'admin.aiUsage.cache' | transloco }}</th>
                <th scope="col">{{ 'admin.aiUsage.fallbacks' | transloco }}</th>
                <th scope="col">{{ 'admin.aiUsage.discards' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (d of u.days; track d.date) {
                <tr>
                  <td>{{ d.date }}</td>
                  <td>{{ d.requests }}</td>
                  <td>{{ d.inputTokens }}</td>
                  <td>{{ d.outputTokens }}</td>
                  <td dir="ltr">\${{ d.estimatedCostUsd.toFixed(4) }}</td>
                  <td>{{ d.cacheHits }}</td>
                  <td>{{ d.fallbacks }}</td>
                  <td>{{ d.discards }}</td>
                </tr>
              }
            </tbody>
          </table>
        </section>
      }
      @if (u.recent.length > 0) {
        <section class="liwa-card">
          <h2>{{ 'admin.aiUsage.recent' | transloco }}</h2>
          <ul>
            @for (r of u.recent; track $index) {
              <li><span class="liwa-muted">{{ when(r.at) }}</span> · {{ 'admin.aiUsage.kinds.' + r.kind | transloco }}: <code>{{ r.reason }}</code></li>
            }
          </ul>
        </section>
      }
    } @else if (usage.error()) {
      <p class="liwa-muted">{{ 'admin.aiUsage.error' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    .stats {
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 12rem), 1fr));
      margin-bottom: 1rem;
    }
    section + section {
      margin-top: 1rem;
    }
  `,
})
export class AiUsagePage {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  protected readonly usage = resource({ loader: () => firstValueFrom(this.http.get<AiUsage>('/api/admin/ai-usage')) });

  protected total(field: 'requests' | 'inputTokens' | 'outputTokens' | 'fallbacks' | 'discards'): number {
    return (this.usage.value()?.days ?? []).reduce((n, d) => n + d[field], 0);
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }
}
