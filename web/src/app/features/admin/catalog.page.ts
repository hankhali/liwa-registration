import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTabsModule } from '@angular/material/tabs';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ConfirmService } from '../../shared/confirm-dialog.component';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { AdminService, ImportReport, Kind, problemTitle } from './admin.service';
import { ImportReportComponent } from './import-report.component';

type Row = Record<string, string | null>;

const CATALOG_KINDS: Kind[] = ['courses', 'degree-plans', 'programs', 'terms', 'sections'];
const PAGE = 50;

/**
 * Catalog (brief §11): courses, degree plans, programs, terms and sections. Every edit, even one field, goes through
 * the importer: Preview shows exactly what an uploaded file would do, Save creates a catalog version. Degree plans are
 * edited as a whole plan; prerequisites are checked live (unknown courses, cycles).
 */
@Component({
  selector: 'app-catalog-page',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTabsModule, TranslocoPipe, PageHeaderComponent, ImportReportComponent],
  template: `
    <app-page-header [heading]="'admin.catalog.heading' | transloco" [subtitle]="'admin.catalog.subtitle' | transloco" />

    <mat-tab-group [selectedIndex]="tab()" (selectedIndexChange)="selectTab($event)" mat-stretch-tabs="false" animationDuration="0ms">
      @for (k of kinds; track k) {
        <mat-tab [label]="'admin.kinds.' + k | transloco"></mat-tab>
      }
    </mat-tab-group>

    <section class="liwa-card" [attr.aria-busy]="data.isLoading()">
      @if (data.hasValue()) {
        @let d = data.value();
        <div class="toolbar">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
            <mat-label>{{ 'admin.catalog.search' | transloco }}</mat-label>
            <input matInput [ngModel]="query()" (ngModelChange)="query.set($event); page.set(0)" />
          </mat-form-field>
          <span class="liwa-muted">{{ 'admin.catalog.count' | transloco: { n: filtered().length } }}</span>
          <button mat-stroked-button type="button" (click)="startAdd(d.columns)"><mat-icon aria-hidden="true">add</mat-icon> {{ 'admin.catalog.add' | transloco }}</button>
        </div>

        <div class="table-wrap">
          <table class="liwa-table dense">
            <thead>
              <tr>
                @for (c of d.columns; track c) {
                  <th scope="col"><code>{{ c }}</code></th>
                }
                <th scope="col"><span class="visually-hidden">{{ 'admin.catalog.actions' | transloco }}</span></th>
              </tr>
            </thead>
            <tbody>
              @for (r of visible(); track $index) {
                <tr>
                  @for (c of d.columns; track c) {
                    <td [attr.dir]="isCode(c) ? 'ltr' : null">{{ r[c] }}</td>
                  }
                  <td class="actions">
                    <button mat-button type="button" (click)="startEdit(r)">{{ 'admin.catalog.edit' | transloco }}</button>
                    @if (kind() === 'degree-plans') {
                      <button mat-button type="button" class="danger" (click)="removePlan(r)">{{ 'admin.catalog.removePlan' | transloco }}</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (filtered().length > PAGE) {
          <div class="pager">
            <button mat-button type="button" [disabled]="page() === 0" (click)="page.set(page() - 1)">{{ 'admin.catalog.prev' | transloco }}</button>
            <span>{{ page() + 1 }} / {{ pages() }}</span>
            <button mat-button type="button" [disabled]="page() + 1 >= pages()" (click)="page.set(page() + 1)">{{ 'admin.catalog.next' | transloco }}</button>
          </div>
        }
        @if (message(); as m) {
          <p class="liwa-notice" [class.error]="m.error" role="status">{{ m.text }}</p>
        }
      } @else if (data.error()) {
        <p class="liwa-muted">{{ 'admin.catalog.loadError' | transloco }}</p>
      } @else {
        <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
      }
    </section>

    @if (editing().length > 0) {
      <section class="liwa-card editor" aria-labelledby="edit-h">
        <h2 id="edit-h">{{ (kind() === 'degree-plans' ? 'admin.catalog.editPlan' : 'admin.catalog.editRow') | transloco }}</h2>
        @if (kind() === 'degree-plans') {
          <p class="liwa-muted">{{ 'admin.catalog.planHint' | transloco }}</p>
        }
        @for (r of editing(); track $index; let i = $index) {
          <fieldset class="edit-row">
            <legend class="visually-hidden">{{ 'admin.catalog.row' | transloco: { n: i + 1 } }}</legend>
            @for (c of columns(); track c) {
              <mat-form-field appearance="outline" subscriptSizing="dynamic" [class.wide]="c === 'prerequisites' || c === 'title' || c === 'name'">
                <mat-label>{{ c }}</mat-label>
                <input matInput [ngModel]="r[c] ?? ''" (ngModelChange)="setCell(i, c, $event)" [attr.dir]="isCode(c) ? 'ltr' : null" />
              </mat-form-field>
            }
            @if (kind() === 'degree-plans') {
              <button mat-icon-button type="button" (click)="removeRow(i)" [attr.aria-label]="'admin.catalog.removeRow' | transloco"><mat-icon aria-hidden="true">delete</mat-icon></button>
            }
          </fieldset>
          @if (kind() === 'courses' && prereq(); as p) {
            <p class="prereq" [class.bad]="!p.ok" role="status">
              @if (p.ok) {
                <mat-icon aria-hidden="true">check</mat-icon> {{ 'admin.catalog.prereqOk' | transloco: { rule: p.canonical ?? ('admin.catalog.none' | transloco) } }}
              } @else {
                <mat-icon aria-hidden="true">error</mat-icon> {{ p.problems.join(' ') }}
              }
            </p>
          }
        }
        <div class="buttons">
          @if (kind() === 'degree-plans') {
            <button mat-button type="button" (click)="addRow()"><mat-icon aria-hidden="true">add</mat-icon> {{ 'admin.catalog.addRow' | transloco }}</button>
          }
          <button mat-stroked-button type="button" [disabled]="busy()" (click)="submit(true)">{{ 'admin.catalog.preview' | transloco }}</button>
          <button mat-flat-button type="button" [disabled]="busy() || !previewOk()" (click)="submit(false)">{{ 'admin.catalog.save' | transloco }}</button>
          <button mat-button type="button" (click)="cancel()">{{ 'plan.confirm.cancel' | transloco }}</button>
        </div>
        @if (report(); as rep) {
          <app-import-report [report]="rep" />
        }
      </section>
    }
  `,
  styles: `
    section {
      margin-top: 1rem;
    }
    .toolbar {
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
      margin-bottom: 0.75rem;
    }
    .search {
      width: min(100%, 20rem);
    }
    .table-wrap {
      overflow-x: auto;
    }
    .dense td,
    .dense th {
      padding: 0.375rem 0.5rem;
      font-size: 0.8125rem;
      white-space: nowrap;
    }
    .actions {
      white-space: nowrap;
    }
    .danger {
      color: var(--liwa-danger);
    }
    .pager {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      justify-content: flex-end;
    }
    .edit-row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
      border: 0;
      padding: 0.5rem 0;
      margin: 0;
      border-bottom: 1px solid var(--liwa-border);
    }
    .edit-row mat-form-field {
      width: 10rem;
    }
    .edit-row mat-form-field.wide {
      width: 20rem;
    }
    .prereq {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      color: var(--liwa-success);
    }
    .prereq.bad {
      color: var(--liwa-danger);
    }
    .buttons {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      margin-top: 1rem;
    }
  `,
})
export class CatalogPage {
  private readonly admin = inject(AdminService);
  private readonly confirm = inject(ConfirmService);
  private readonly transloco = inject(TranslocoService);

  protected readonly PAGE = PAGE;
  protected readonly kinds = CATALOG_KINDS;
  protected readonly tab = signal(0);
  protected readonly kind = computed(() => CATALOG_KINDS[this.tab()]);
  protected readonly data = resource({ params: () => this.kind(), loader: ({ params }) => this.admin.rows(params) });
  protected readonly query = signal('');
  protected readonly page = signal(0);
  protected readonly editing = signal<Row[]>([]);
  protected readonly columns = signal<string[]>([]);
  protected readonly report = signal<ImportReport | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<{ text: string; error: boolean } | null>(null);
  protected readonly prereq = signal<{ ok: boolean; canonical: string | null; problems: string[] } | null>(null);
  private prereqTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const rows = this.data.value()?.rows ?? [];
    return q ? rows.filter((r) => Object.values(r).some((v) => (v ?? '').toLowerCase().includes(q))) : rows;
  });
  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.filtered().length / PAGE)));
  protected readonly visible = computed(() => this.filtered().slice(this.page() * PAGE, (this.page() + 1) * PAGE));
  protected readonly previewOk = computed(() => {
    const r = this.report();
    return !!r && r.dryRun && r.errorCount === 0;
  });

  protected isCode(c: string): boolean {
    return c.endsWith('_code') || c === 'prerequisites' || c === 'section' || c === 'student_id' || c === 'username';
  }

  protected selectTab(i: number): void {
    this.tab.set(i);
    this.query.set('');
    this.page.set(0);
    this.cancel();
  }

  protected startEdit(r: Row): void {
    const d = this.data.value();
    if (!d) return;
    this.columns.set(d.columns);
    // A degree plan is replaced as a whole: edit every row of that program + catalog year.
    const rows = this.kind() === 'degree-plans'
      ? d.rows.filter((x) => x['program_code'] === r['program_code'] && x['catalog_year'] === r['catalog_year'])
      : [r];
    this.editing.set(rows.map((x) => ({ ...x })));
    this.report.set(null);
    this.message.set(null);
    this.checkPrereq();
  }

  protected startAdd(columns: string[]): void {
    this.columns.set(columns);
    this.editing.set([Object.fromEntries(columns.map((c) => [c, null]))]);
    this.report.set(null);
    this.prereq.set(null);
  }

  protected setCell(i: number, c: string, value: string): void {
    this.editing.update((rows) => rows.map((r, j) => (j === i ? { ...r, [c]: value === '' ? null : value } : r)));
    this.report.set(null);
    if (c === 'prerequisites' || c === 'course_code') this.checkPrereq();
  }

  protected addRow(): void {
    const first = this.editing()[0] ?? {};
    this.editing.update((rows) => [...rows, { ...first, course_code: null, suggested_semester: null }]);
  }

  protected removeRow(i: number): void {
    this.editing.update((rows) => rows.filter((_, j) => j !== i));
    this.report.set(null);
  }

  protected cancel(): void {
    this.editing.set([]);
    this.report.set(null);
    this.prereq.set(null);
  }

  /** Live rule check while typing (debounced). */
  private checkPrereq(): void {
    if (this.kind() !== 'courses') return;
    clearTimeout(this.prereqTimer);
    const r = this.editing()[0];
    if (!r) return;
    this.prereqTimer = setTimeout(async () => {
      try {
        this.prereq.set(await this.admin.checkPrerequisite(r['course_code'] ?? '', r['prerequisites'] ?? ''));
      } catch {
        this.prereq.set(null);
      }
    }, 350);
  }

  protected async submit(dryRun: boolean): Promise<void> {
    this.busy.set(true);
    try {
      const rep = await this.admin.editRows(this.kind(), this.editing(), dryRun);
      this.report.set(rep);
      if (!dryRun && rep.saved) {
        this.message.set({ text: this.transloco.translate('admin.report.saved', { v: rep.catalogVersion }), error: false });
        this.editing.set([]);
        this.data.reload();
      }
    } finally {
      this.busy.set(false);
    }
  }

  protected async removePlan(r: Row): Promise<void> {
    const program = r['program_code'] ?? '';
    const year = Number(r['catalog_year']);
    const ok = await this.confirm.ask({
      title: this.transloco.translate('admin.catalog.removeTitle', { program, year }),
      body: this.transloco.translate('admin.catalog.removeBody'),
      confirm: this.transloco.translate('admin.catalog.removePlan'),
      cancel: this.transloco.translate('plan.confirm.cancel'),
      danger: true,
    });
    if (!ok) return;
    try {
      await this.admin.removeDegreePlan(program, year);
      this.message.set({ text: this.transloco.translate('admin.catalog.removed', { program, year }), error: false });
      this.data.reload();
    } catch (e) {
      this.message.set({ text: problemTitle(e) ?? this.transloco.translate('admin.catalog.removeFailed'), error: true });
    }
  }
}
