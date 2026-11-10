import { Component, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoPipe } from '@jsverse/transloco';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { AdminService, ImportReport, KINDS, Kind } from './admin.service';
import { ImportReportComponent } from './import-report.component';

/**
 * Imports and exports (brief §11): choose a file → check it (dry run with what would be added, updated, unchanged,
 * removed) → apply. Any error rejects the whole file. Exports are one file or a zip, and import again unchanged.
 */
@Component({
  selector: 'app-imports-page',
  imports: [MatButtonModule, MatIconModule, MatSelectModule, TranslocoPipe, PageHeaderComponent, ImportReportComponent],
  template: `
    <app-page-header [heading]="'admin.imports.heading' | transloco" [subtitle]="'admin.imports.subtitle' | transloco" />

    <section class="liwa-card" aria-labelledby="upload-h">
      <h2 id="upload-h">{{ 'admin.imports.upload' | transloco }}</h2>
      <ol class="steps">
        <li>{{ 'admin.imports.step1' | transloco }}</li>
        <li>{{ 'admin.imports.step2' | transloco }}</li>
        <li>{{ 'admin.imports.step3' | transloco }}</li>
      </ol>
      <div class="row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'admin.imports.kind' | transloco }}</mat-label>
          <mat-select [value]="kind()" (valueChange)="kind.set($event); reset()">
            @for (k of kinds; track k) {
              <mat-option [value]="k">{{ 'admin.kinds.' + k | transloco }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <label class="file">
          <input type="file" accept=".csv,text/csv" (change)="pick($event)" />
          <span>{{ file()?.name ?? ('admin.imports.chooseFile' | transloco) }}</span>
        </label>
        <button mat-stroked-button type="button" [disabled]="!file() || busy()" (click)="run(true)">
          <mat-icon aria-hidden="true">fact_check</mat-icon> {{ 'admin.imports.check' | transloco }}
        </button>
        <button mat-flat-button type="button" [disabled]="!canApply() || busy()" (click)="run(false)">
          <mat-icon aria-hidden="true">upload</mat-icon> {{ 'admin.imports.apply' | transloco }}
        </button>
      </div>
      <p class="liwa-muted small">{{ 'admin.imports.order' | transloco }}</p>
      @if (report(); as r) {
        <app-import-report [report]="r" />
      }
      @if (error()) {
        <p class="liwa-notice error" role="alert">{{ 'admin.imports.failed' | transloco }}</p>
      }
    </section>

    <section class="liwa-card" aria-labelledby="export-h">
      <h2 id="export-h">{{ 'admin.imports.exports' | transloco }}</h2>
      <p class="liwa-muted">{{ 'admin.imports.exportsHint' | transloco }}</p>
      <div class="row wrap">
        <button mat-flat-button type="button" (click)="exportAll()"><mat-icon aria-hidden="true">archive</mat-icon> {{ 'admin.imports.zip' | transloco }}</button>
        @for (k of kinds; track k) {
          <button mat-stroked-button type="button" (click)="exportOne(k)">{{ 'admin.kinds.' + k | transloco }}</button>
        }
      </div>
    </section>

    <section class="liwa-card" aria-labelledby="versions-h">
      <h2 id="versions-h">{{ 'admin.versions.heading' | transloco }}</h2>
      @if (versions.hasValue()) {
        <div class="table-wrap">
          <table class="liwa-table">
            <thead>
              <tr>
                <th scope="col">#</th>
                <th scope="col">{{ 'admin.versions.when' | transloco }}</th>
                <th scope="col">{{ 'admin.versions.what' | transloco }}</th>
                <th scope="col">{{ 'admin.versions.source' | transloco }}</th>
                <th scope="col">{{ 'admin.versions.who' | transloco }}</th>
                <th scope="col">{{ 'admin.versions.changes' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (v of versions.value(); track v.number) {
                <tr>
                  <td>{{ v.number }}</td>
                  <td>{{ when(v.at) }}</td>
                  <td>{{ 'admin.kinds.' + v.kind | transloco }}@if (v.fileName) { <span class="liwa-muted small"> · {{ v.fileName }}</span>}</td>
                  <td>{{ 'admin.versions.sources.' + v.source | transloco }}</td>
                  <td>{{ v.actorName ?? ('history.system' | transloco) }}</td>
                  <td>{{ 'admin.versions.counts' | transloco: v }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styles: `
    section + section {
      margin-top: 1rem;
    }
    .steps {
      margin: 0 0 1rem;
      padding-inline-start: 1.25rem;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .file input {
      position: absolute;
      opacity: 0;
      width: 1px;
      height: 1px;
    }
    .file span {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
      padding: 0 1rem;
      border: 1px dashed var(--liwa-border);
      border-radius: var(--liwa-radius);
      cursor: pointer;
    }
    .file:focus-within span {
      outline: 3px solid var(--liwa-accent);
    }
    .small {
      font-size: 0.8125rem;
    }
    .table-wrap {
      overflow-x: auto;
    }
  `,
})
export class ImportsPage {
  private readonly admin = inject(AdminService);
  private readonly language = inject(LanguageService);

  protected readonly kinds = KINDS;
  protected readonly kind = signal<Kind>('courses');
  protected readonly file = signal<File | null>(null);
  protected readonly report = signal<ImportReport | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal(false);
  protected readonly versions = resource({ loader: () => this.admin.versions() });

  protected canApply(): boolean {
    const r = this.report();
    return !!this.file() && !!r && r.dryRun && r.errorCount === 0;
  }

  protected pick(e: Event): void {
    this.file.set((e.target as HTMLInputElement).files?.[0] ?? null);
    this.report.set(null);
  }

  protected reset(): void {
    this.report.set(null);
  }

  protected async run(dryRun: boolean): Promise<void> {
    const f = this.file();
    if (!f) return;
    this.busy.set(true);
    this.error.set(false);
    try {
      this.report.set(await this.admin.upload(this.kind(), f, dryRun));
      if (!dryRun) this.versions.reload();
    } catch {
      this.error.set(true);
    } finally {
      this.busy.set(false);
    }
  }

  protected exportOne(k: Kind): Promise<void> {
    return this.admin.download(`/api/admin/exports/${k}`, `${k}.csv`);
  }

  protected exportAll(): Promise<void> {
    return this.admin.download('/api/admin/exports', 'liwa-data.zip');
  }

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }
}
