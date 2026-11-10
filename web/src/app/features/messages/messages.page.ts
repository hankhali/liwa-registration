import { HttpClient } from '@angular/common/http';
import { Component, inject, resource } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { NotificationItem, NotificationList } from '../../core/planning/advisor.models';
import { EmptyStateComponent } from '../../shared/empty-state.component';
import { PageHeaderComponent } from '../../shared/page-header.component';

/** Every notification for the signed-in person, newest first, in their language (any role). */
@Component({
  selector: 'app-messages-page',
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslocoPipe, EmptyStateComponent, PageHeaderComponent],
  template: `
    <app-page-header [heading]="'messages.heading' | transloco" [subtitle]="'messages.subtitle' | transloco">
      @if (list.value()?.unread) {
        <button mat-stroked-button type="button" (click)="markRead()">{{ 'messages.markRead' | transloco }}</button>
      }
    </app-page-header>

    @if (list.hasValue()) {
      @if (list.value().items.length === 0) {
        <app-empty-state icon="mail" [heading]="'messages.empty' | transloco" [body]="'messages.emptyBody' | transloco" />
      } @else {
        <ul class="messages liwa-card">
          @for (n of list.value().items; track n.id) {
            <li [class.unread]="!n.read">
              <mat-icon aria-hidden="true">{{ n.read ? 'drafts' : 'mail' }}</mat-icon>
              <div>
                @if (n.link) {
                  <a [routerLink]="n.link">{{ text(n) }}</a>
                } @else {
                  <span>{{ text(n) }}</span>
                }
                <small class="liwa-muted">{{ when(n.createdAt) }}@if (!n.read) { · {{ 'messages.new' | transloco }}}</small>
              </div>
            </li>
          }
        </ul>
      }
    } @else if (list.error()) {
      <p class="liwa-muted">{{ 'messages.error' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    .messages {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    li {
      display: flex;
      gap: 0.75rem;
      padding: 0.875rem 1.25rem;
      border-bottom: 1px solid var(--liwa-border);
    }
    li:last-child {
      border-bottom: 0;
    }
    li div {
      display: flex;
      flex-direction: column;
    }
    .unread a,
    .unread span {
      font-weight: 700;
    }
    mat-icon {
      color: var(--liwa-accent);
      flex: none;
    }
  `,
})
export class MessagesPage {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  protected readonly list = resource({ loader: () => firstValueFrom(this.http.get<NotificationList>('/api/notifications')) });

  protected text(n: NotificationItem): string {
    this.language.lang();
    let params: Record<string, unknown> = {};
    try {
      params = n.parameters ? (JSON.parse(n.parameters) as Record<string, unknown>) : {};
    } catch {
      /* show without names */
    }
    return this.transloco.translate(n.messageKey, params);
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  protected async markRead(): Promise<void> {
    await firstValueFrom(this.http.post('/api/notifications/read', null));
    this.list.reload();
  }
}
