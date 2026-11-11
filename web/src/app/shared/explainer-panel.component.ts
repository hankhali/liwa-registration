import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, computed, inject, input, output, resource, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { LanguageService } from '../core/i18n/language.service';
import { graduationDate, parseTermCode, termLabel } from '../core/planning/planning.models';

export interface ExplainFacts {
  intent: string;
  course: string | null;
  before: { graduationWithoutSummer: string | null; graduationWithSummer: string | null };
  after: { graduationWithoutSummer: string | null; graduationWithSummer: string | null };
  semesterDelta: number;
  newProblems: { code: string; courses: string[]; value: string | null }[];
}

export interface ExplainAnswer {
  available: boolean;
  text: string | null;
  fallback: boolean;
  intent: string | null;
  facts: ExplainFacts | null;
  cached: boolean;
  rateLimited: boolean;
}

/**
 * The AI explainer (brief §12): "Explain my plan", "Why not this course?" and a question box limited to the plan.
 * It only explains what the planner produced; for what-if questions the planner's own figures are shown under the
 * answer. Advisors also get "Draft a note to the student", which goes into their note box to edit — never sent.
 * With no AI provider configured the panel is hidden and nothing else changes.
 */
@Component({
  selector: 'app-explainer-panel',
  imports: [FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, TranslocoPipe],
  template: `
    @if (available()) {
    <section class="explainer liwa-card" aria-labelledby="ex-h" #panel>
      <h3 id="ex-h"><mat-icon aria-hidden="true">auto_awesome</mat-icon> {{ 'explainer.heading' | transloco }}</h3>
      @if (status.hasValue()) {
        <p class="liwa-muted small">{{ 'explainer.intro' | transloco }}</p>
        <div class="actions">
          <button mat-stroked-button type="button" [disabled]="busy()" (click)="ask('Plan')">{{ 'explainer.explainPlan' | transloco }}</button>
          @if (planId()) {
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="ask('DraftNote')">{{ 'explainer.draftNote' | transloco }}</button>
          }
        </div>
        <form class="question" (ngSubmit)="ask('Question')">
          <label class="visually-hidden" for="ex-q">{{ 'explainer.questionLabel' | transloco }}</label>
          <input id="ex-q" name="q" [(ngModel)]="question" maxlength="300" [placeholder]="'explainer.placeholder' | transloco" />
          <button mat-flat-button type="submit" [disabled]="busy() || !question.trim()">{{ 'explainer.ask' | transloco }}</button>
        </form>

        @if (busy()) {
          <p class="loading" role="status"><mat-spinner diameter="18" aria-hidden="true" /> {{ 'explainer.thinking' | transloco }}</p>
        }
        @if (answer(); as a) {
          <div class="answer" [class.fallback]="a.fallback" role="status" aria-live="polite">
            <p>{{ a.text }}</p>
            @if (a.facts; as f) {
              <div class="facts">
                <strong>{{ 'explainer.facts' | transloco }}</strong>
                @if (f.intent === 'summer') {
                  <p>{{ 'explainer.factsSummer' | transloco: { with: term(f.before.graduationWithSummer), without: term(f.before.graduationWithoutSummer) } }}</p>
                } @else {
                  <p>{{ 'explainer.factsChange' | transloco: { before: term(f.before.graduationWithoutSummer), after: term(f.after.graduationWithoutSummer) } }}</p>
                }
                <p>{{ (f.semesterDelta > 0 ? 'explainer.later' : f.semesterDelta < 0 ? 'explainer.earlier' : 'explainer.same') | transloco: { n: abs(f.semesterDelta) } }}</p>
              </div>
            }
            @if (lastKind() === 'DraftNote' && !a.fallback && a.text) {
              <button mat-button type="button" (click)="useNote.emit(a.text)">{{ 'explainer.useNote' | transloco }}</button>
            }
            <p class="liwa-muted small">{{ 'explainer.disclaimer' | transloco }}</p>
          </div>
        }
      }
    </section>
    }
  `,
  styles: `
    .explainer {
      margin-top: 1rem;
      border-inline-start: 4px solid var(--liwa-accent);
    }
    h3 {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0 0 0.25rem;
    }
    .small {
      font-size: 0.8125rem;
    }
    .actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
      margin: 0.5rem 0;
    }
    .question {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .question input {
      flex: 1 1 16rem;
      min-height: 40px;
      padding: 0 0.75rem;
      border: 1px solid var(--liwa-border);
      border-radius: var(--liwa-radius);
      background: var(--liwa-surface);
      color: var(--liwa-text);
      font: inherit;
    }
    .loading {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .answer {
      margin-top: 0.75rem;
      padding: 0.75rem 1rem;
      border-radius: var(--liwa-radius);
      background: var(--liwa-page);
    }
    .answer.fallback {
      border-inline-start: 4px solid var(--liwa-warning);
    }
    .facts {
      margin-top: 0.5rem;
      padding-top: 0.5rem;
      border-top: 1px solid var(--liwa-border);
      font-size: 0.9375rem;
    }
    .facts p {
      margin: 0.25rem 0;
    }
  `,
})
export class ExplainerPanelComponent {
  /** Student: the sections on screen (their selection). */
  readonly sectionIds = input<string[]>([]);
  /** Advisor: the plan under review (also enables "Draft a note"). */
  readonly planId = input<string | null>(null);
  readonly useNote = output<string>();

  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  protected readonly status = resource({ loader: () => firstValueFrom(this.http.get<{ available: boolean }>('/api/me/explainer')) });
  /** With no AI provider the panel is not shown at all (an empty "Not available" box only confuses). */
  readonly available = computed(() => this.status.hasValue() && this.status.value().available);
  protected readonly answer = signal<ExplainAnswer | null>(null);
  protected readonly busy = signal(false);
  protected readonly lastKind = signal<string | null>(null);
  protected question = '';

  /** "Why not this course?" from a not-eligible row. */
  async whyNot(courseCode: string): Promise<void> {
    this.panel()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
    await this.send({ kind: 'WhyNot', courseCode });
  }

  protected ask(kind: 'Plan' | 'Question' | 'DraftNote'): Promise<void> {
    return this.send({ kind, question: kind === 'Question' ? this.question : null });
  }

  private async send(body: { kind: string; courseCode?: string; question?: string | null }): Promise<void> {
    this.busy.set(true);
    this.lastKind.set(body.kind);
    try {
      const request = { ...body, language: this.language.lang(), sectionIds: this.planId() ? null : this.sectionIds() };
      const url = this.planId() ? `/api/advisor/plans/${this.planId()}/explain` : '/api/students/me/explain';
      this.answer.set(await firstValueFrom(this.http.post<ExplainAnswer>(url, request)));
    } catch {
      this.answer.set({ available: true, text: this.transloco.translate('explainer.error'), fallback: true, intent: null, facts: null, cached: false, rateLimited: false });
    } finally {
      this.busy.set(false);
    }
  }

  protected term(code: string | null): string {
    return code && parseTermCode(code) ? graduationDate(code, (k) => this.transloco.translate(k), this.language.lang()) : this.transloco.translate('advisor.review.unknownTerm');
  }

  protected abs(n: number): number {
    return Math.abs(n);
  }
}
