import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { GraduationProjection, Reason, courseTitle, criticalCodes, parseTermCode, semesterDelay } from '../../core/planning/planning.models';
import { PlanningService } from '../../core/planning/planning.service';
import { ReasonTextPipe } from '../../core/planning/reason-text.pipe';
import { PageHeaderComponent } from '../../shared/page-header.component';

/**
 * Graduation Path (brief §9): semesters as columns, courses as cards, gold = critical path with the legend as a
 * sentence; a Summer toggle; "My selection / Recommended" with both graduation dates when they differ; a banner
 * when the selection delays graduation; and a "Why some courses wait" list.
 */
@Component({
  selector: 'app-graduation-page',
  imports: [MatButtonToggleModule, MatIconModule, MatSlideToggleModule, RouterLink, TranslocoPipe, ReasonTextPipe, PageHeaderComponent],
  template: `
    <app-page-header [heading]="'graduation.heading' | transloco" [subtitle]="'graduation.subtitle' | transloco" />

    @if (data.hasValue()) {
      @let d = data.value();
      <section class="liwa-card controls">
        <mat-slide-toggle [checked]="summer()" (change)="summer.set($event.checked)">{{ 'graduation.summer' | transloco }}</mat-slide-toggle>
        @if (d.selection) {
          <mat-button-toggle-group [value]="mode()" (change)="mode.set($event.value)" [attr.aria-label]="'graduation.compare' | transloco">
            <mat-button-toggle value="selection">{{ 'graduation.mySelection' | transloco }}</mat-button-toggle>
            <mat-button-toggle value="recommended">{{ 'graduation.recommended' | transloco }}</mat-button-toggle>
          </mat-button-toggle-group>
        }
        <p class="dates">
          <mat-icon aria-hidden="true">school</mat-icon>
          @if (d.selection && selectionTerm() !== recommendedTerm()) {
            <span>
              {{ 'graduation.both' | transloco: { mine: label(selectionTerm()), rec: label(recommendedTerm()) } }}
            </span>
          } @else {
            <span>{{ 'graduation.date' | transloco: { term: label(shownTerm()) } }}</span>
          }
        </p>
      </section>

      @if (delay() > 0) {
        <p class="liwa-notice warn banner" role="status">
          <mat-icon aria-hidden="true">warning</mat-icon>
          {{ (delay() === 1 ? 'graduation.delayOne' : 'graduation.delay') | transloco: { n: delay() } }}
          <a routerLink="/student/planner">{{ 'graduation.fix' | transloco }}</a>
        </p>
      }

      <p class="legend"><span class="liwa-critical"><mat-icon aria-hidden="true" class="inline-icon">star</mat-icon>{{ 'plan.critical' | transloco }}</span> {{ 'graduation.legend' | transloco }}</p>

      <div class="columns" role="list" [attr.aria-label]="'graduation.columns' | transloco">
        @for (t of terms(); track t.termCode; let first = $first) {
          <section class="term liwa-card" role="listitem" [attr.aria-labelledby]="'t-' + t.termCode">
            <h2 [id]="'t-' + t.termCode">{{ label(t.termCode) }}@if (first) { <span class="now">{{ 'graduation.now' | transloco }}</span>}</h2>
            <p class="liwa-muted small">{{ 'plan.credits' | transloco: { n: t.credits } }}</p>
            <ul>
              @for (c of t.courses; track c) {
                <li class="card" [class.critical]="critical().has(c)">
                  <bdi class="code" dir="ltr">{{ c }}</bdi>
                  <span>{{ title(c) }}</span>
                  @if (critical().has(c)) {
                    <span class="visually-hidden">{{ 'plan.critical' | transloco }}</span>
                  }
                </li>
              }
              @if (t.electiveCredits > 0) {
                <li class="card elective">{{ 'graduation.electives' | transloco: { n: t.electiveCredits } }}</li>
              }
            </ul>
          </section>
        } @empty {
          <p class="liwa-muted">{{ 'graduation.none' | transloco }}</p>
        }
      </div>

      @if (waiting().length > 0) {
        <section class="liwa-card wait" aria-labelledby="wait-heading">
          <h2 id="wait-heading">{{ 'graduation.whyWait' | transloco }}</h2>
          <ul>
            @for (w of waiting(); track w.code) {
              <li>
                <bdi class="code" dir="ltr">{{ w.code }}</bdi> {{ title(w.code) }} — {{ label(w.term) }}:
                @if (w.reason) {
                  {{ w.reason | reasonText }}
                } @else {
                  {{ 'graduation.creditLimit' | transloco }}
                }
              </li>
            }
          </ul>
        </section>
      }
    } @else if (data.error()) {
      <p class="liwa-muted">{{ 'plan.notReady' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    .controls {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1rem 1.5rem;
      margin-bottom: 1rem;
    }
    .dates {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
      font-weight: 600;
    }
    .banner {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
      font-weight: 600;
    }
    .legend {
      margin: 0.5rem 0 1rem;
    }
    .inline-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }
    .columns {
      display: grid;
      grid-auto-flow: column;
      grid-auto-columns: minmax(12rem, 1fr);
      gap: 0.75rem;
      overflow-x: auto;
      padding-bottom: 0.5rem;
    }
    .term {
      padding: 0.875rem;
    }
    .term h2 {
      font-size: 1rem;
      margin: 0;
    }
    .now {
      margin-inline-start: 0.5rem;
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--liwa-accent);
    }
    .small {
      font-size: 0.8125rem;
      margin: 0 0 0.5rem;
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.375rem;
    }
    .card {
      display: flex;
      flex-direction: column;
      padding: 0.375rem 0.5rem;
      border-radius: 6px;
      background: var(--liwa-block);
      font-size: 0.8125rem;
      border-inline-start: 4px solid var(--liwa-border);
    }
    .card.critical {
      background: var(--liwa-gold-bg);
      color: var(--liwa-gold-fg);
      border-inline-start-color: var(--liwa-gold-fg);
    }
    .card.elective {
      font-style: italic;
    }
    .code {
      font-weight: 700;
    }
    .wait {
      margin-top: 1rem;
    }
    .wait ul {
      display: block;
      padding-inline-start: 1.25rem;
      list-style: disc;
    }
    .wait li {
      margin: 0.25rem 0;
    }
  `,
})
export class GraduationPage {
  private readonly planning = inject(PlanningService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  protected readonly summer = signal(false);
  protected readonly mode = signal<'selection' | 'recommended'>('selection');

  protected readonly data = resource({
    loader: async () => {
      const prefs = await this.planning.preferences();
      const view = await this.planning.view(prefs.availability, prefs.delivery, prefs.maxCredits);
      const ids = view.saved?.sectionIds.length ? view.saved.sectionIds : prefs.draftSectionIds;
      const recommendedIds = view.recommendation.plan.map((p) => p.sectionId);
      const differs = ids.length > 0 && [...ids].sort().join() !== [...recommendedIds].sort().join();
      const selection: GraduationProjection | null = differs ? (view.savedProjection ?? (await this.planning.projection(ids))) : null;
      return { view, selection };
    },
  });

  private readonly proj = computed(() => {
    if (!this.data.hasValue()) return null;
    const d = this.data.value();
    const p = this.mode() === 'selection' && d.selection ? d.selection : d.view.recommendation.projection;
    return this.summer() ? p.withSummer : p.withoutSummer;
  });

  protected readonly terms = computed(() => this.proj()?.terms ?? []);
  protected readonly shownTerm = computed(() => this.proj()?.graduationTerm ?? null);
  protected readonly recommendedTerm = computed(() => {
    const p = this.data.value()?.view.recommendation.projection;
    return (this.summer() ? p?.withSummer : p?.withoutSummer)?.graduationTerm ?? null;
  });
  protected readonly selectionTerm = computed(() => {
    const p = this.data.value()?.selection;
    return (this.summer() ? p?.withSummer : p?.withoutSummer)?.graduationTerm ?? null;
  });
  protected readonly delay = computed(() => (this.data.value()?.selection ? semesterDelay(this.recommendedTerm(), this.selectionTerm(), this.summer()) : 0));
  protected readonly critical = computed(() => criticalCodes(this.data.value()?.view.recommendation.courses ?? []));

  /** Courses that wait for a later semester, with the planner's own reason (prerequisite, season, credit limit). */
  protected readonly waiting = computed(() => {
    const d = this.data.value();
    if (!d) return [];
    const later = this.terms().slice(1);
    const out: { code: string; term: string; reason: Reason | null }[] = [];
    for (const t of later)
      for (const code of t.courses) {
        const a = d.view.recommendation.courses.find((c) => c.courseCode === code);
        const reason = a?.reasons.find((r) => !['RequiredForDegree', 'CountsTowardElectives', 'SuggestedForThisSemester', 'UnlocksOtherCourses'].includes(r.code)) ?? null;
        out.push({ code, term: t.termCode, reason });
      }
    return out.slice(0, 40);
  });

  protected label(code: string | null): string {
    this.language.lang();
    const t = code ? parseTermCode(code) : null;
    return t ? `${this.transloco.translate('planner.season.' + t.season)} ${t.year}` : this.transloco.translate('advisor.review.unknownTerm');
  }

  protected title(code: string): string {
    const c = this.data.value()?.view.courses.find((x) => x.code === code);
    return courseTitle(c, this.language.lang());
  }
}
