import { Component, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  AvailabilityPreset,
  DeliveryPreference,
  PRESETS,
  PRESET_CARDS,
  TEACHING_DAYS,
  TimeWindow,
} from '../../core/planning/planning.models';
import { PlanningService } from '../../core/planning/planning.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { TimetableGridComponent } from '../../shared/timetable-grid.component';

/**
 * "When are you free?" (brief §9). First run: four preset cards, one line each, and a way to skip.
 * After a choice the week grid appears, already filled in, and the student can fine-tune it hour by hour.
 */
@Component({
  selector: 'app-availability-page',
  imports: [MatButtonModule, MatIconModule, MatSelectModule, RouterLink, TranslocoPipe, PageHeaderComponent, TimetableGridComponent],
  template: `
    <app-page-header [heading]="'availability.heading' | transloco" [subtitle]="'availability.intro' | transloco" />

    @if (prefs.hasValue()) {
      <section class="liwa-card" aria-labelledby="presets-heading">
        <h2 id="presets-heading" class="visually-hidden">{{ 'availability.presetsHeading' | transloco }}</h2>
        <div class="presets" role="radiogroup" [attr.aria-label]="'availability.presetsHeading' | transloco">
          @for (p of cards; track p) {
            <button type="button" class="preset" role="radio" [class.selected]="preset() === p" [attr.aria-checked]="preset() === p" (click)="choose(p)">
              <mat-icon aria-hidden="true">{{ icons[p] }}</mat-icon>
              <span class="preset-title">{{ 'availability.presets.' + p + '.title' | transloco }}</span>
              <span class="preset-body">{{ 'availability.presets.' + p + '.body' | transloco }}</span>
            </button>
          }
        </div>
        <p class="skip">
          <button mat-button type="button" (click)="skip()">{{ 'availability.skip' | transloco }}</button>
        </p>
      </section>

      @if (preset() && preset() !== 'any') {
        <section class="liwa-card grid-card" aria-labelledby="grid-heading">
          <h2 id="grid-heading">{{ 'availability.gridHeading' | transloco }}</h2>
          <p class="liwa-muted">{{ 'availability.gridHint' | transloco }}</p>
          <app-timetable-grid [editable]="true" [days]="gridDays()" [(free)]="windows" [caption]="'availability.gridHeading' | transloco" />

          <div class="prefs">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'plan.prefs.delivery' | transloco }}</mat-label>
              <mat-select [value]="delivery()" (valueChange)="delivery.set($event)">
                <mat-option value="Any">{{ 'plan.prefs.deliveryAny' | transloco }}</mat-option>
                <mat-option value="OnCampusOnly">{{ 'plan.prefs.deliveryCampus' | transloco }}</mat-option>
                <mat-option value="OnlineOnly">{{ 'plan.prefs.deliveryOnline' | transloco }}</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'plan.prefs.maxCredits' | transloco }}</mat-label>
              <mat-select [value]="maxCredits()" (valueChange)="maxCredits.set($event)">
                <mat-option [value]="null">{{ 'plan.prefs.noLimit' | transloco }}</mat-option>
                @for (n of [6, 9, 12, 15, 18]; track n) {
                  <mat-option [value]="n">{{ 'plan.prefs.upTo' | transloco: { n: n } }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>

          <div class="actions">
            <button mat-flat-button type="button" [disabled]="busy() || windows().length === 0" (click)="save()">
              <mat-icon aria-hidden="true">check</mat-icon> {{ 'availability.save' | transloco }}
            </button>
            @if (windows().length === 0) {
              <span class="liwa-muted">{{ 'availability.pickSome' | transloco }}</span>
            }
          </div>
        </section>
      }

      @if (error()) {
        <p class="liwa-notice error" role="alert">{{ 'availability.error' | transloco }}</p>
      }
    } @else if (prefs.error()) {
      <p class="liwa-muted">{{ 'availability.loadError' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
    <p class="next"><a routerLink="/student/planner">{{ 'availability.toPlanner' | transloco }}</a></p>
  `,
  styles: `
    .presets {
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 13rem), 1fr));
    }
    .preset {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.25rem;
      min-height: 7.5rem;
      padding: 1rem;
      text-align: start;
      font: inherit;
      color: inherit;
      background: var(--liwa-surface);
      border: 2px solid var(--liwa-border);
      border-radius: var(--liwa-radius);
      cursor: pointer;
    }
    .preset:hover {
      border-color: var(--liwa-accent);
    }
    .preset.selected {
      border-color: var(--liwa-primary);
      background: var(--liwa-block);
    }
    .preset mat-icon {
      color: var(--liwa-accent);
    }
    .preset-title {
      font-weight: 700;
      font-size: 1.0625rem;
    }
    .preset-body {
      color: var(--liwa-text-muted);
      font-size: 0.9375rem;
    }
    .skip {
      margin: 0.75rem 0 0;
    }
    .grid-card {
      margin-top: 1rem;
    }
    .prefs {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      margin: 1rem 0;
    }
    .actions {
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .next {
      margin-top: 1rem;
    }
  `,
})
export class AvailabilityPage {
  private readonly planning = inject(PlanningService);
  private readonly router = inject(Router);

  protected readonly cards = PRESET_CARDS;
  protected readonly icons: Record<string, string> = { daytime: 'light_mode', evenings: 'nights_stay', weekends: 'weekend', custom: 'tune' };

  protected readonly preset = signal<AvailabilityPreset | null>(null);
  protected readonly windows = signal<TimeWindow[]>([]);
  protected readonly delivery = signal<DeliveryPreference>('Any');
  protected readonly maxCredits = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal(false);

  protected readonly prefs = resource({
    loader: async () => {
      const p = await this.planning.preferences();
      this.preset.set(p.preset);
      this.windows.set(p.availability);
      this.delivery.set(p.delivery);
      this.maxCredits.set(p.maxCredits);
      return p;
    },
  });

  /** Saturday shows only when the free time includes it (it's off by default for teaching). */
  protected gridDays() {
    return this.windows().some((w) => w.day === 'Saturday') || this.preset() === 'weekends'
      ? [...TEACHING_DAYS, 'Saturday' as const]
      : TEACHING_DAYS;
  }

  protected choose(p: AvailabilityPreset): void {
    this.preset.set(p);
    this.windows.set(p === 'custom' ? [] : PRESETS[p as Exclude<AvailabilityPreset, 'custom'>].map((w) => ({ ...w })));
  }

  /** "Skip this to see every class." */
  protected async skip(): Promise<void> {
    this.preset.set('any');
    this.windows.set([]);
    await this.persist();
  }

  protected async save(): Promise<void> {
    // Hand edits to a preset make it a custom choice.
    const p = this.preset();
    const same = p && p !== 'custom' && p !== 'any' && JSON.stringify(PRESETS[p]) === JSON.stringify(this.windows());
    if (!same) this.preset.set('custom');
    await this.persist();
  }

  private async persist(): Promise<void> {
    this.busy.set(true);
    this.error.set(false);
    try {
      await this.planning.savePreferences(this.preset() ?? 'any', this.windows(), this.delivery(), this.maxCredits());
      await this.router.navigateByUrl('/student/planner');
    } catch {
      this.error.set(true);
    } finally {
      this.busy.set(false);
    }
  }
}
