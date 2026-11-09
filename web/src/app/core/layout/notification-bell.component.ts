import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { formatDateTime } from '../academic/academic.models';
import { LanguageService } from '../i18n/language.service';
import { NotificationItem, NotificationList } from '../planning/advisor.models';

/** Bell in the top bar: unread count, latest messages in the reader's language, mark all read on open. */
@Component({
  selector: 'app-notification-bell',
  imports: [MatBadgeModule, MatButtonModule, MatIconModule, MatMenuModule, TranslocoPipe],
  template: `
    <button
      mat-icon-button
      class="bell"
      type="button"
      [matMenuTriggerFor]="menu"
      (menuOpened)="opened()"
      [attr.aria-label]="'notificationsUi.label' | transloco: { count: list().unread }"
    >
      <mat-icon aria-hidden="true" [matBadge]="list().unread || null" matBadgeColor="accent" matBadgeSize="small">notifications</mat-icon>
    </button>
    <mat-menu #menu="matMenu" xPosition="before" class="notification-menu">
      @if (list().items.length === 0) {
        <p class="empty">{{ 'notificationsUi.empty' | transloco }}</p>
      }
      @for (n of list().items; track n.id) {
        <button mat-menu-item type="button" (click)="go(n)" [class.unread]="!n.read">
          <span class="text">
            <span>{{ n.messageKey | transloco: params(n) }}</span>
            <small>{{ when(n.createdAt) }}</small>
          </span>
        </button>
      }
    </mat-menu>
  `,
  styles: `
    .bell {
      --mat-icon-button-icon-color: #fff;
    }
    .empty {
      margin: 0;
      padding: 0.75rem 1rem;
      color: var(--liwa-text-muted);
    }
    .text {
      display: flex;
      flex-direction: column;
      white-space: normal;
      line-height: 1.35;
      padding: 0.25rem 0;
      small {
        color: var(--liwa-text-muted);
      }
    }
    .unread .text span:first-child {
      font-weight: 700;
    }
  `,
})
export class NotificationBellComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  protected readonly list = signal<NotificationList>({ unread: 0, items: [] });

  constructor() {
    void this.load();
    const timer = setInterval(() => void this.load(), 60_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected async opened(): Promise<void> {
    if (this.list().unread === 0) return;
    try {
      await firstValueFrom(this.http.post('/api/notifications/read', null));
    } catch {
      /* try again next time */
    }
  }

  protected go(n: NotificationItem): void {
    void this.load();
    if (n.link) void this.router.navigateByUrl(n.link);
  }

  protected params(n: NotificationItem): Record<string, unknown> {
    try {
      return n.parameters ? (JSON.parse(n.parameters) as Record<string, unknown>) : {};
    } catch {
      return {};
    }
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  private async load(): Promise<void> {
    try {
      this.list.set(await firstValueFrom(this.http.get<NotificationList>('/api/notifications')));
    } catch {
      /* not signed in or offline: keep the last list */
    }
  }
}
