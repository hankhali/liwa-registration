import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { AdminService, AiSettings, CreditPolicy, CreditPolicyPreview, CreditRule, SsoSettings, TestResult, problemTitle } from './admin.service';

/**
 * Settings (brief §11): university sign-in (Entra ID) and the AI provider, each with "Test connection", and the credit
 * rules with a test that lists every student whose limits would change before anything is saved. Secrets are only
 * ever shown as their last four characters. Username/password sign-in can't be switched off by accident.
 */
@Component({
  selector: 'app-settings-page',
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, MatSlideToggleModule, TranslocoPipe, PageHeaderComponent],
  template: `
    <app-page-header [heading]="'admin.settings.heading' | transloco" [subtitle]="'admin.settings.subtitle' | transloco" />

    <!-- Sign-in -->
    <section class="liwa-card" aria-labelledby="sso-h">
      <h2 id="sso-h">{{ 'admin.settings.sso.heading' | transloco }}</h2>
      @if (sso.hasValue()) {
        @let s = sso.value();
        <p class="liwa-muted">{{ 'admin.settings.sso.now' | transloco: { sso: (s.effectiveEnabled ? 'admin.settings.on' : 'admin.settings.off') | transloco, local: (s.effectiveLocalSignIn ? 'admin.settings.on' : 'admin.settings.off') | transloco } }}</p>
        <div class="form">
          <mat-slide-toggle [(ngModel)]="ssoForm.enabled">{{ 'admin.settings.sso.enabled' | transloco }}</mat-slide-toggle>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>{{ 'admin.settings.sso.tenant' | transloco }}</mat-label><input matInput dir="ltr" [(ngModel)]="ssoForm.tenantId" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>{{ 'admin.settings.sso.client' | transloco }}</mat-label><input matInput dir="ltr" [(ngModel)]="ssoForm.clientId" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'admin.settings.sso.secret' | transloco }}</mat-label>
            <input matInput type="password" dir="ltr" [(ngModel)]="ssoForm.clientSecret" [placeholder]="s.clientSecret || ''" autocomplete="off" />
            <mat-hint>{{ s.clientSecret ? ('admin.settings.stored' | transloco: { last: s.clientSecret }) : ('admin.settings.notStored' | transloco) }}</mat-hint>
          </mat-form-field>
        </div>
        <mat-slide-toggle [(ngModel)]="ssoForm.localSignInEnabled">{{ 'admin.settings.sso.local' | transloco }}</mat-slide-toggle>
        @if (!ssoForm.localSignInEnabled) {
          <p class="liwa-notice warn">
            {{ 'admin.settings.sso.ssoOnlyWarning' | transloco }}
            <mat-checkbox [(ngModel)]="ssoForm.confirmSsoOnly">{{ 'admin.settings.sso.confirm' | transloco }}</mat-checkbox>
          </p>
        }
        <p class="liwa-muted small">{{ 'admin.settings.sso.restart' | transloco }}
          @if (s.lastTestOkAt) { {{ 'admin.settings.sso.lastOk' | transloco: { when: when(s.lastTestOkAt) } }} }
        </p>
        <div class="buttons">
          <button mat-flat-button type="button" (click)="saveSso()">{{ 'admin.settings.save' | transloco }}</button>
          <button mat-stroked-button type="button" (click)="testSso()"><mat-icon aria-hidden="true">network_check</mat-icon> {{ 'admin.settings.test' | transloco }}</button>
        </div>
        @if (ssoMsg(); as m) {
          <p class="liwa-notice" [class.error]="!m.ok" [class.ok]="m.ok" role="status">{{ m.message }}</p>
        }
      }
    </section>

    <!-- AI provider -->
    <section class="liwa-card" aria-labelledby="ai-h">
      <h2 id="ai-h">{{ 'admin.settings.ai.heading' | transloco }}</h2>
      <p class="liwa-muted">{{ 'admin.settings.ai.hint' | transloco }}</p>
      @if (ai.hasValue()) {
        @let a = ai.value();
        <div class="form">
          <mat-slide-toggle [(ngModel)]="aiForm.enabled">{{ 'admin.settings.ai.enabled' | transloco }}</mat-slide-toggle>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'admin.settings.ai.provider' | transloco }}</mat-label>
            <mat-select [(ngModel)]="aiForm.provider">
              <mat-option value="anthropic">Anthropic (Claude)</mat-option>
              <mat-option value="openai-compatible">{{ 'admin.settings.ai.openai' | transloco }}</mat-option>
              <mat-option value="mock">{{ 'admin.settings.ai.mock' | transloco }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>{{ 'admin.settings.ai.model' | transloco }}</mat-label><input matInput dir="ltr" [(ngModel)]="aiForm.model" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide"><mat-label>{{ 'admin.settings.ai.baseUrl' | transloco }}</mat-label><input matInput dir="ltr" [(ngModel)]="aiForm.baseUrl" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'admin.settings.ai.key' | transloco }}</mat-label>
            <input matInput type="password" dir="ltr" [(ngModel)]="aiForm.apiKey" autocomplete="off" />
            <mat-hint>{{ a.apiKey ? ('admin.settings.stored' | transloco: { last: a.apiKey }) : ('admin.settings.notStored' | transloco) }}</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="narrow"><mat-label>{{ 'admin.settings.ai.timeout' | transloco }}</mat-label><input matInput type="number" [(ngModel)]="aiForm.timeoutSeconds" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="narrow"><mat-label>{{ 'admin.settings.ai.limit' | transloco }}</mat-label><input matInput type="number" [(ngModel)]="aiForm.hourlyLimitPerUser" /></mat-form-field>
        </div>
        <div class="buttons">
          <button mat-flat-button type="button" (click)="saveAi()">{{ 'admin.settings.save' | transloco }}</button>
          <button mat-stroked-button type="button" (click)="testAi()"><mat-icon aria-hidden="true">network_check</mat-icon> {{ 'admin.settings.test' | transloco }}</button>
        </div>
        @if (aiMsg(); as m) {
          <p class="liwa-notice" [class.error]="!m.ok" [class.ok]="m.ok" role="status">{{ m.message }}</p>
        }
      }
    </section>

    <!-- Credit rules -->
    <section class="liwa-card" aria-labelledby="credit-h">
      <h2 id="credit-h">{{ 'admin.settings.credit.heading' | transloco }}</h2>
      <p class="liwa-muted">{{ 'admin.settings.credit.hint' | transloco }}</p>
      @if (policyForm(); as p) {
        <div class="form">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'admin.settings.credit.repeat' | transloco }}</mat-label>
            <mat-select [(ngModel)]="p.repeatGradeRule">
              <mat-option value="LatestCounts">{{ 'admin.settings.credit.latest' | transloco }}</mat-option>
              <mat-option value="HighestCounts">{{ 'admin.settings.credit.highest' | transloco }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-checkbox [(ngModel)]="p.allowInProgressPrerequisites">{{ 'admin.settings.credit.inProgress' | transloco }}</mat-checkbox>
          <mat-checkbox [(ngModel)]="p.graduatingStudentsExemptFromMinimum">{{ 'admin.settings.credit.exempt' | transloco }}</mat-checkbox>
        </div>
        <div class="table-wrap">
          <table class="liwa-table rules">
            <thead>
              <tr>
                <th scope="col">#</th>
                <th scope="col">{{ 'admin.settings.credit.name' | transloco }}</th>
                <th scope="col">{{ 'admin.settings.credit.season' | transloco }}</th>
                <th scope="col">{{ 'admin.settings.credit.standing' | transloco }}</th>
                <th scope="col">{{ 'admin.settings.credit.minGpa' | transloco }}</th>
                <th scope="col">{{ 'admin.settings.credit.maxGpa' | transloco }}</th>
                <th scope="col">{{ 'admin.settings.credit.probation' | transloco }}</th>
                <th scope="col">{{ 'admin.settings.credit.min' | transloco }}</th>
                <th scope="col">{{ 'admin.settings.credit.max' | transloco }}</th>
                <th scope="col"><span class="visually-hidden">{{ 'admin.catalog.actions' | transloco }}</span></th>
              </tr>
            </thead>
            <tbody>
              @for (r of p.rules; track $index; let i = $index) {
                <tr>
                  <td>{{ i + 1 }}</td>
                  <td><input class="cell wide" [(ngModel)]="r.name" [attr.aria-label]="'admin.settings.credit.name' | transloco" /></td>
                  <td>
                    <select class="cell" [(ngModel)]="r.season" [attr.aria-label]="'admin.settings.credit.season' | transloco">
                      <option [ngValue]="null">{{ 'admin.settings.credit.any' | transloco }}</option>
                      @for (s of seasons; track s) {<option [ngValue]="s">{{ 'planner.season.' + s | transloco }}</option>}
                    </select>
                  </td>
                  <td>
                    <select class="cell" [(ngModel)]="r.standing" [attr.aria-label]="'admin.settings.credit.standing' | transloco">
                      <option [ngValue]="null">{{ 'admin.settings.credit.any' | transloco }}</option>
                      @for (s of standings; track s) {<option [ngValue]="s">{{ 'student.home.studies.standing.' + s | transloco }}</option>}
                    </select>
                  </td>
                  <td><input class="cell num" type="number" step="0.01" [(ngModel)]="r.minGpa" [attr.aria-label]="'admin.settings.credit.minGpa' | transloco" /></td>
                  <td><input class="cell num" type="number" step="0.01" [(ngModel)]="r.maxGpaExclusive" [attr.aria-label]="'admin.settings.credit.maxGpa' | transloco" /></td>
                  <td>
                    <select class="cell" [(ngModel)]="r.onProbation" [attr.aria-label]="'admin.settings.credit.probation' | transloco">
                      <option [ngValue]="null">{{ 'admin.settings.credit.any' | transloco }}</option>
                      <option [ngValue]="true">{{ 'admin.settings.credit.yes' | transloco }}</option>
                      <option [ngValue]="false">{{ 'admin.settings.credit.no' | transloco }}</option>
                    </select>
                  </td>
                  <td><input class="cell num" type="number" [(ngModel)]="r.minCredits" [attr.aria-label]="'admin.settings.credit.min' | transloco" /></td>
                  <td><input class="cell num" type="number" [(ngModel)]="r.maxCredits" [attr.aria-label]="'admin.settings.credit.max' | transloco" /></td>
                  <td class="nowrap">
                    <button mat-icon-button type="button" (click)="moveRule(i, -1)" [disabled]="i === 0" [attr.aria-label]="'admin.settings.credit.up' | transloco"><mat-icon aria-hidden="true">arrow_upward</mat-icon></button>
                    <button mat-icon-button type="button" (click)="moveRule(i, 1)" [disabled]="i === p.rules.length - 1" [attr.aria-label]="'admin.settings.credit.down' | transloco"><mat-icon aria-hidden="true">arrow_downward</mat-icon></button>
                    <button mat-icon-button type="button" (click)="removeRule(i)" [attr.aria-label]="'admin.catalog.removeRow' | transloco"><mat-icon aria-hidden="true">delete</mat-icon></button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="buttons">
          <button mat-button type="button" (click)="addRule()"><mat-icon aria-hidden="true">add</mat-icon> {{ 'admin.settings.credit.add' | transloco }}</button>
          <button mat-stroked-button type="button" (click)="previewPolicy()">{{ 'admin.settings.credit.test' | transloco }}</button>
          <button mat-flat-button type="button" [disabled]="!preview() || preview()!.problems.length > 0" (click)="savePolicy()">{{ 'admin.settings.save' | transloco }}</button>
        </div>
        @if (preview(); as pv) {
          <div class="liwa-notice" [class.error]="pv.problems.length > 0" role="status">
            @if (pv.problems.length > 0) {
              <ul>@for (x of pv.problems; track $index) {<li>{{ x }}</li>}</ul>
            } @else if (pv.changes.length === 0) {
              {{ 'admin.settings.credit.noChanges' | transloco: { n: pv.studentsChecked, term: pv.termName } }}
            } @else {
              <strong>{{ 'admin.settings.credit.changes' | transloco: { n: pv.changes.length, total: pv.studentsChecked, term: pv.termName } }}</strong>
              <table class="liwa-table">
                <tbody>
                  @for (c of pv.changes; track c.studentId) {
                    <tr>
                      <td><bdi dir="ltr">{{ c.studentId }}</bdi> {{ c.studentName }}</td>
                      <td>{{ c.beforeRule ?? '—' }} ({{ c.beforeMin ?? '—' }}–{{ c.beforeMax ?? '—' }})</td>
                      <td>→ {{ c.afterRule ?? '—' }} ({{ c.afterMin ?? '—' }}–{{ c.afterMax ?? '—' }})</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        }
        @if (policyMsg()) {
          <p class="liwa-notice ok" role="status">{{ policyMsg() }}</p>
        }
      }
    </section>
  `,
  styles: `
    section {
      margin-top: 1rem;
    }
    .form {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem 1rem;
      align-items: center;
      margin-bottom: 0.75rem;
    }
    .form mat-form-field {
      width: 15rem;
    }
    .form mat-form-field.wide {
      width: 24rem;
    }
    .form mat-form-field.narrow {
      width: 9rem;
    }
    .buttons {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-top: 0.75rem;
    }
    .small {
      font-size: 0.8125rem;
    }
    .table-wrap {
      overflow-x: auto;
    }
    .cell {
      min-height: 34px;
      padding: 0 0.375rem;
      border: 1px solid var(--liwa-border);
      border-radius: 6px;
      background: var(--liwa-surface);
      color: var(--liwa-text);
      font: inherit;
      font-size: 0.875rem;
    }
    .cell.wide {
      width: 14rem;
    }
    .cell.num {
      width: 5rem;
    }
    .nowrap {
      white-space: nowrap;
    }
  `,
})
export class SettingsPage {
  private readonly admin = inject(AdminService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  protected readonly seasons = ['Fall', 'Spring', 'Summer'] as const;
  protected readonly standings = ['Freshman', 'Sophomore', 'Junior', 'Senior'] as const;

  protected ssoForm = { enabled: false, tenantId: '', clientId: '', clientSecret: '', localSignInEnabled: true, confirmSsoOnly: false };
  protected aiForm: AiSettings = { enabled: false, provider: 'anthropic', model: 'claude-sonnet-5-5', baseUrl: null, apiKey: null, timeoutSeconds: 20, hourlyLimitPerUser: 20 };
  protected readonly ssoMsg = signal<TestResult | null>(null);
  protected readonly aiMsg = signal<TestResult | null>(null);
  protected readonly policyForm = signal<CreditPolicy | null>(null);
  protected readonly preview = signal<CreditPolicyPreview | null>(null);
  protected readonly policyMsg = signal<string | null>(null);

  protected readonly sso = resource({
    loader: async () => {
      const s = await this.admin.sso();
      this.fillSso(s);
      return s;
    },
  });
  protected readonly ai = resource({
    loader: async () => {
      const a = await this.admin.ai();
      this.aiForm = { ...a, apiKey: null };
      return a;
    },
  });
  protected readonly policy = resource({
    loader: async () => {
      const p = await this.admin.creditPolicy();
      this.policyForm.set(structuredClone(p));
      return p;
    },
  });

  private fillSso(s: SsoSettings): void {
    this.ssoForm = { enabled: s.enabled, tenantId: s.tenantId ?? '', clientId: s.clientId ?? '', clientSecret: '', localSignInEnabled: s.localSignInEnabled, confirmSsoOnly: false };
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  protected async saveSso(): Promise<void> {
    try {
      const s = await this.admin.saveSso({ ...this.ssoForm, tenantId: this.ssoForm.tenantId || null, clientId: this.ssoForm.clientId || null, clientSecret: this.ssoForm.clientSecret || null });
      this.fillSso(s);
      this.ssoMsg.set({ ok: true, message: this.transloco.translate('admin.settings.saved') });
      this.sso.reload();
    } catch (e) {
      this.ssoMsg.set({ ok: false, message: problemTitle(e) ?? this.transloco.translate('admin.settings.failed') });
    }
  }

  protected async testSso(): Promise<void> {
    this.ssoMsg.set(await this.admin.testSso().catch(() => ({ ok: false, message: this.transloco.translate('admin.settings.failed') })));
    this.sso.reload();
  }

  protected async saveAi(): Promise<void> {
    try {
      await this.admin.saveAi({ ...this.aiForm, apiKey: this.aiForm.apiKey || null });
      this.aiMsg.set({ ok: true, message: this.transloco.translate('admin.settings.saved') });
      this.ai.reload();
    } catch (e) {
      this.aiMsg.set({ ok: false, message: problemTitle(e) ?? this.transloco.translate('admin.settings.failed') });
    }
  }

  protected async testAi(): Promise<void> {
    this.aiMsg.set(await this.admin.testAi().catch((e: unknown): TestResult => ({ ok: false, message: problemTitle(e) ?? this.transloco.translate('admin.settings.failed') })));
  }

  protected addRule(): void {
    this.policyForm.update((p) => p && { ...p, rules: [...p.rules, { name: '', season: null, standing: null, minGpa: null, maxGpaExclusive: null, onProbation: null, minCredits: 12, maxCredits: 18 } as CreditRule] });
    this.preview.set(null);
  }

  protected removeRule(i: number): void {
    this.policyForm.update((p) => p && { ...p, rules: p.rules.filter((_, j) => j !== i) });
    this.preview.set(null);
  }

  protected moveRule(i: number, by: number): void {
    this.policyForm.update((p) => {
      if (!p) return p;
      const rules = [...p.rules];
      [rules[i], rules[i + by]] = [rules[i + by], rules[i]];
      return { ...p, rules };
    });
    this.preview.set(null);
  }

  protected async previewPolicy(): Promise<void> {
    const p = this.policyForm();
    if (p) this.preview.set(await this.admin.previewCreditPolicy(this.clean(p)));
    this.policyMsg.set(null);
  }

  protected async savePolicy(): Promise<void> {
    const p = this.policyForm();
    if (!p) return;
    const r = await this.admin.saveCreditPolicy(this.clean(p));
    this.preview.set(r);
    if (r.problems.length === 0) {
      this.policyMsg.set(this.transloco.translate('admin.settings.credit.saved'));
      this.policy.reload();
    }
  }

  /** Empty number inputs become null; numbers come back as numbers. */
  private clean(p: CreditPolicy): CreditPolicy {
    const n = (v: unknown) => (v === '' || v === null || v === undefined ? null : Number(v));
    return {
      ...p,
      rules: p.rules.map((r) => ({ ...r, minGpa: n(r.minGpa), maxGpaExclusive: n(r.maxGpaExclusive), minCredits: Number(r.minCredits), maxCredits: Number(r.maxCredits) })),
    };
  }
}
