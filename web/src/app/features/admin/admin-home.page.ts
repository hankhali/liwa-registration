import { HttpClient } from '@angular/common/http';
import { Component, inject, resource } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';

interface SystemStatus {
  version: string;
  environment: string;
  database: string;
  demoSeedEnabled: boolean;
}

/** Administrator landing page. The only role that sees system information. */
@Component({
  selector: 'app-admin-home',
  imports: [MatIconModule, TranslocoPipe],
  template: `
    <header class="page-header">
      <h1>{{ 'admin.home.heading' | transloco }}</h1>
      <p class="liwa-muted">{{ 'admin.home.subtitle' | transloco }}</p>
    </header>

    <div class="grid">
      <section class="liwa-card" aria-labelledby="status-heading" [attr.aria-busy]="status.isLoading()">
        <h2 id="status-heading" class="card-title">
          <mat-icon aria-hidden="true">verified</mat-icon>
          {{ 'admin.home.systemStatus' | transloco }}
        </h2>
        @if (status.hasValue()) {
          @let s = status.value();
          @let dbOk = s.database === 'Healthy';
          <!-- One plain-language answer first; technical details only for IT, folded away. -->
          @if (dbOk) {
            <p class="summary summary-ok" role="status">
              <mat-icon aria-hidden="true">check_circle</mat-icon>
              {{ 'admin.home.allGood' | transloco }}
            </p>
          } @else {
            <p class="summary summary-bad" role="alert">
              <mat-icon aria-hidden="true">error</mat-icon>
              {{ 'admin.home.dbDown' | transloco }}
            </p>
          }
          @if (s.demoSeedEnabled) {
            <p class="summary summary-warn">
              <mat-icon aria-hidden="true">warning</mat-icon>
              {{ 'admin.home.demoWarning' | transloco }}
            </p>
          }
          <details class="details">
            <summary><mat-icon aria-hidden="true">expand_more</mat-icon>{{ 'admin.home.details' | transloco }}</summary>
          <dl class="status-list">
            <div>
              <dt>{{ 'admin.home.database' | transloco }}</dt>
              <dd>
                <span class="pill" [class.pill-ok]="s.database === 'Healthy'" [class.pill-bad]="s.database !== 'Healthy'">
                  {{ (s.database === 'Healthy' ? 'admin.home.healthy' : 'admin.home.unhealthy') | transloco }}
                </span>
              </dd>
            </div>
            <div>
              <dt>{{ 'admin.home.environment' | transloco }}</dt>
              <dd dir="ltr">{{ s.environment }}</dd>
            </div>
            <div>
              <dt>{{ 'admin.home.version' | transloco }}</dt>
              <dd dir="ltr">{{ s.version }}</dd>
            </div>
            <div>
              <dt>{{ 'admin.home.demoAccounts' | transloco }}</dt>
              <dd>
                <span class="pill" [class.pill-warn]="s.demoSeedEnabled">
                  {{ (s.demoSeedEnabled ? 'admin.home.demoOn' : 'admin.home.demoOff') | transloco }}
                </span>
              </dd>
            </div>
          </dl>
          </details>
        } @else if (status.error()) {
          <p class="error" role="alert">{{ 'admin.home.statusError' | transloco }}</p>
        } @else {
          <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
        }
      </section>

      <section class="liwa-card" aria-labelledby="manage-heading">
        <h2 id="manage-heading" class="card-title">
          <mat-icon aria-hidden="true">tune</mat-icon>
          {{ 'admin.home.manage' | transloco }}
        </h2>
        <ul class="areas">
          @for (area of areas; track area) {
            <li>{{ 'admin.home.areas.' + area | transloco }}</li>
          }
        </ul>
        <p class="liwa-muted">{{ 'admin.home.manageComingSoon' | transloco }}</p>
      </section>
    </div>
  `,
  styleUrls: ['../../shared/page.scss', './admin-home.page.scss'],
})
export class AdminHomePage {
  private readonly http = inject(HttpClient);
  protected readonly status = resource({ loader: () => firstValueFrom(this.http.get<SystemStatus>('/api/admin/system')) });
  protected readonly areas = ['terms', 'catalog', 'degreePlans', 'creditPolicy', 'imports'];
}
