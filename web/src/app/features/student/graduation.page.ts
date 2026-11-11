import { Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { localDigits } from '../../core/i18n/digits';
import { GraduationProjection, Reason, courseTitle, criticalCodes, parseTermCode, semesterDelay, termLabel } from '../../core/planning/planning.models';
import { isolate } from '../../shared/bidi';
import { PlanningService } from '../../core/planning/planning.service';
import { ReasonTextPipe } from '../../core/planning/reason-text.pipe';
import { PageHeaderComponent } from '../../shared/page-header.component';

/**
 * Graduation Path (brief §9, UX pass): the answer as a headline (graduation date and semesters left) with one
 * toggle, "Include summer classes"; the student's own plan when they have one, with our recommended date in one line
 * when it differs; a banner when their choice delays graduation; semesters as columns (stacked on phones), gold =
 * take on time; and "Why some classes wait", collapsed.
 */
@Component({
  selector: 'app-graduation-page',
  imports: [MatButtonModule, MatIconModule, MatSlideToggleModule, RouterLink, TranslocoPipe, ReasonTextPipe, PageHeaderComponent],
  template: `
    <app-page-header [heading]="'graduation.heading' | transloco" [subtitle]="'graduation.subtitle' | transloco">
      @if (delay() > 0) {
        <a mat-flat-button routerLink="/student/planner">{{ 'graduation.fix' | transloco }}</a>
      }
    </app-page-header>

    @if (data.hasValue()) {
      @let d = data.value();
      <!-- The answer in a headline, and the one toggle that changes it -->
      <section class="liwa-card headline" aria-labelledby="grad-date">
        <div class="big">
          <span class="big-label">{{ 'graduation.dateLabel' | transloco }}</span>
          <span id="grad-date" class="big-value" aria-live="polite">{{ label(shownTerm()) }}</span>
        </div>
        <div class="big">
          <span class="big-label">{{ 'graduation.leftLabel' | transloco }}</span>
          <span class="big-value">{{ digits(terms().length) }}</span>
        </div>
        <div class="toggle">
          <mat-slide-toggle [checked]="summer()" (change)="summer.set($event.checked)">{{ 'graduation.summer' | transloco }}</mat-slide-toggle>
          @if (summerOnly(); as courses) {
            <!-- The toggle changes nothing: a required class only runs in summer, so the path has that summer anyway -->
            <span class="summer-note" role="note">{{ (courses.count === 1 ? 'graduation.summerOnlyOne' : 'graduation.summerOnly') | transloco: { courses: courses.text } }}</span>
          } @else {
            <span class="liwa-muted small">{{ (summer() ? 'graduation.summerOn' : 'graduation.summerOff') | transloco: { term: label(otherTerm()) } }}</span>
          }
        </div>
        @if (fullTimeTerm() !== undefined) {
          <!-- Free time is limited: the date if it continues, next to a full-time schedule -->
          <p class="compare keep" role="note">
            {{ 'graduation.keepFreeTime' | transloco: { freeTime: ('graduation.freeTime.' + (d.preset ?? 'custom') | transloco), term: label(shownTerm()), fullTime: label(fullTimeTerm() ?? null) } }}
          </p>
          @if (d.view.recommendation.freeTime; as ft) {
            <p class="compare liwa-muted small assumes">{{ (ft.onlinePerTerm === 1 ? 'graduation.assumesOnlineOne' : 'graduation.assumesOnline') | transloco: { n: ft.onlinePerTerm } }}</p>
          }
        }
        @if (d.selection && selectionTerm() !== recommendedTerm()) {
          <p class="compare liwa-muted">{{ 'graduation.recommendedLine' | transloco: { term: label(recommendedTerm()) } }}</p>
        }
      </section>

      @if (delay() > 0) {
        <p class="liwa-notice warn banner" role="status">
          <mat-icon aria-hidden="true">warning</mat-icon>
          {{ (delay() === 1 ? 'graduation.delayOne' : 'graduation.delay') | transloco: { n: delay() } }}
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
                <li class="card elective">{{ (t.electiveCredits === 1 ? 'graduation.electivesOne' : 'graduation.electives') | transloco: { n: t.electiveCredits } }}</li>
              }
            </ul>
          </section>
        } @empty {
          <p class="liwa-muted">{{ 'graduation.none' | transloco }}</p>
        }
      </div>

      @if (waiting().length > 0) {
        <details class="liwa-card wait">
          <summary><h2>{{ 'graduation.whyWait' | transloco: { n: waiting().length } }}</h2></summary>
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
        </details>
      }
    } @else if (data.error()) {
      <p class="liwa-muted">{{ 'plan.notReady' | transloco }}</p>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,  styles: `
    .headline {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1rem 2.5rem;
      margin-bottom: 1rem;
      border-top: 4px solid var(--liwa-gold);
    }
    .big {
      display: flex;
      flex-direction: column;
    }
    .big-label {
      color: var(--liwa-text-muted);
      font-size: 0.875rem;
    }
    .big-value {
      font-size: 2.25rem;
      font-weight: 700;
      line-height: 1.15;
      color: var(--liwa-primary);
    }
    .toggle {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .compare {
      flex-basis: 100%;
      margin: 0;
    }
    .keep {
      font-weight: 600;
    }
    .summer-note {
      max-width: 34rem;
      font-size: 0.9375rem;
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
    .wait summary {
      cursor: pointer;
      min-height: 44px;
      display: flex;
      align-items: center;
    }
    .wait summary h2 {
      display: inline;
      margin: 0;
      font-size: 1.0625rem;
    }
    @media (max-width: 600px) {
      .columns {
        grid-auto-flow: row;
        grid-auto-columns: auto;
        overflow-x: visible;
      }
      .big-value {
        font-size: 1.75rem;
      }
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

  protected readonly data = resource({
    loader: async () => {
      const prefs = await this.planning.preferences();
      const view = await this.planning.view(prefs.availability, prefs.delivery, prefs.maxCredits);
      const ids = view.saved?.sectionIds.length ? view.saved.sectionIds : prefs.draftSectionIds;
      const recommendedIds = view.recommendation.plan.map((p) => p.sectionId);
      const differs = ids.length > 0 && [...ids].sort().join() !== [...recommendedIds].sort().join();
      const selection: GraduationProjection | null = differs ? (view.savedProjection ?? (await this.planning.projection(ids))) : null;
      return { view, selection, preset: prefs.preset };
    },
  });

  private readonly proj = computed(() => {
    if (!this.data.hasValue()) return null;
    const d = this.data.value();
    const p = d.selection ?? d.view.recommendation.projection;
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
  /** With limited free time: the graduation date with a full-time schedule from next semester (undefined = not limited). */
  protected readonly fullTimeTerm = computed(() => {
    const f = this.data.value()?.view.recommendation.fullTimeProjection;
    if (!f) return undefined;
    return (this.summer() ? f.withSummer : f.withoutSummer).graduationTerm;
  });

  /** The date with the toggle the other way ("with summer you could finish in …"). */
  protected readonly otherTerm = computed(() => {
    const d = this.data.value();
    if (!d) return null;
    const p = d.selection ?? d.view.recommendation.projection;
    return (this.summer() ? p.withoutSummer : p.withSummer).graduationTerm;
  });
  /**
   * When "Include summer classes" can't change the date because the path has a summer anyway: a required class is
   * only offered in summer (e.g. an internship). Returns those classes as "BUS410 Business Internship", or null.
   */
  protected readonly summerOnly = computed(() => {
    const d = this.data.value();
    if (!d) return null;
    const p = d.selection ?? d.view.recommendation.projection;
    if (p.withSummer.graduationTerm !== p.withoutSummer.graduationTerm) return null;
    const codes = (p.withoutSummer.terms ?? [])
      .filter((t) => parseTermCode(t.termCode)?.season === 'Summer')
      .flatMap((t) => t.courses);
    if (codes.length === 0) return null;
    const unique = [...new Set(codes)];
    return { count: unique.length, text: unique.map((c) => `${isolate(c)} ${this.title(c)}`.trim()).join(', ') };
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
    return code ? termLabel(code, (k) => this.transloco.translate(k), this.language.lang()) : this.transloco.translate('advisor.review.unknownTerm');
  }

  protected digits(n: number): string {
    return localDigits(String(n), this.language.lang());
  }

  protected title(code: string): string {
    const c = this.data.value()?.view.courses.find((x) => x.code === code);
    return courseTitle(c, this.language.lang());
  }
}
