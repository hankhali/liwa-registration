import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { formatDateTime, programName } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { DECIDABLE, QueueItem } from '../../core/planning/advisor.models';

type StatusFilter = 'waiting' | 'all' | 'Approved' | 'Rejected' | 'ChangesProposed';
type Sort = 'oldest' | 'newest' | 'name';

/** Review queue: status and program filters with counts, sort orders, urgency markers; cards on phones. */
@Component({
  selector: 'app-advisor-queue',
  imports: [MatFormFieldModule, MatSelectModule, RouterLink, TranslocoPipe],
  template: `
    <header class="page-header">
      <h1>{{ 'advisor.queue.heading' | transloco }}</h1>
      <p class="liwa-muted">{{ 'advisor.queue.subtitle' | transloco }}</p>
    </header>

    <div class="filters" role="group" [attr.aria-label]="'advisor.queue.filterLabel' | transloco">
      @for (f of statusFilters; track f) {
        <button type="button" class="filter" [class.on]="status() === f" [attr.aria-pressed]="status() === f" (click)="status.set(f)">
          {{ 'advisor.queue.filters.' + f | transloco }} <span class="count">{{ count(f) }}</span>
        </button>
      }
    </div>

    <div class="controls">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'advisor.queue.program' | transloco }}</mat-label>
        <mat-select [value]="program()" (valueChange)="program.set($event)">
          <mat-option value="">{{ 'advisor.queue.allPrograms' | transloco }}</mat-option>
          @for (p of programs(); track p.code) {
            <mat-option [value]="p.code">{{ p.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'advisor.queue.sort' | transloco }}</mat-label>
        <mat-select [value]="sort()" (valueChange)="sort.set($event)">
          <mat-option value="oldest">{{ 'advisor.queue.sorts.oldest' | transloco }}</mat-option>
          <mat-option value="newest">{{ 'advisor.queue.sorts.newest' | transloco }}</mat-option>
          <mat-option value="name">{{ 'advisor.queue.sorts.name' | transloco }}</mat-option>
        </mat-select>
      </mat-form-field>
    </div>

    @if (queue.hasValue()) {
      @if (rows().length === 0) {
        <p class="empty">{{ 'advisor.queue.empty' | transloco }}</p>
      } @else {
        <table class="queue">
          <thead>
            <tr>
              <th scope="col">{{ 'advisor.queue.cols.student' | transloco }}</th>
              <th scope="col">{{ 'advisor.queue.cols.program' | transloco }}</th>
              <th scope="col">{{ 'advisor.queue.cols.status' | transloco }}</th>
              <th scope="col">{{ 'advisor.queue.cols.credits' | transloco }}</th>
              <th scope="col">{{ 'advisor.queue.cols.updated' | transloco }}</th>
            </tr>
          </thead>
          <tbody>
            @for (q of rows(); track q.planId) {
              <tr [class.urgent]="q.urgent">
                <td>
                  <a [routerLink]="['/advisor/plans', q.planId]">{{ q.studentName }}</a>
                  <span class="sub" dir="ltr">{{ q.studentId }}</span>
                  @if (q.urgent) {
                    <span class="flag">{{ 'advisor.queue.urgent' | transloco }}</span>
                  }
                </td>
                <td>{{ programLabel(q) }}</td>
                <td><span class="status s-{{ q.status }}">{{ 'planStatus.' + q.status | transloco }}</span></td>
                <td>
                  {{ q.credits }}
                  @if (q.warningCount > 0) {
                    <span class="sub">{{ 'advisor.queue.notes' | transloco: { n: q.warningCount } }}</span>
                  }
                </td>
                <td class="sub">{{ when(q.updatedAt) }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    } @else if (queue.error()) {
      <p class="empty">{{ 'advisor.queue.error' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styleUrls: ['../../shared/page.scss', './advisor-queue.page.scss'],
})
export class AdvisorQueuePage {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);

  protected readonly statusFilters: StatusFilter[] = ['waiting', 'ChangesProposed', 'Rejected', 'Approved', 'all'];
  protected readonly status = signal<StatusFilter>('waiting');
  protected readonly program = signal('');
  protected readonly sort = signal<Sort>('oldest');
  protected readonly queue = resource({ loader: () => firstValueFrom(this.http.get<QueueItem[]>('/api/advisor/plans')) });

  protected readonly programs = computed(() => {
    const map = new Map<string, string>();
    for (const q of this.queue.value() ?? []) map.set(q.programCode, programName(q, this.language.lang()));
    return [...map].map(([code, name]) => ({ code, name }));
  });

  protected readonly rows = computed(() => {
    const items = (this.queue.value() ?? []).filter((q) => this.matches(q, this.status()) && (!this.program() || q.programCode === this.program()));
    const sort = this.sort();
    return [...items].sort((a, b) =>
      sort === 'name'
        ? a.studentName.localeCompare(b.studentName)
        : sort === 'newest'
          ? b.updatedAt.localeCompare(a.updatedAt)
          : Number(b.urgent) - Number(a.urgent) || a.updatedAt.localeCompare(b.updatedAt),
    );
  });

  protected count(f: StatusFilter): number {
    return (this.queue.value() ?? []).filter((q) => this.matches(q, f) && (!this.program() || q.programCode === this.program())).length;
  }

  protected programLabel(q: QueueItem): string {
    return programName(q, this.language.lang());
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  private matches(q: QueueItem, f: StatusFilter): boolean {
    return f === 'all' || (f === 'waiting' ? DECIDABLE.includes(q.status) : q.status === f);
  }
}
