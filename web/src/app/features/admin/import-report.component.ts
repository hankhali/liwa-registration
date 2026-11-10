import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { ImportReport } from './admin.service';

/** What an import or an edit would do (dry run) or did: counts per change, then every problem by line and column. */
@Component({
  selector: 'app-import-report',
  imports: [MatIconModule, TranslocoPipe],
  template: `
    @let r = report();
    <div class="report" [class.has-errors]="r.errorCount > 0" role="status">
      <p class="headline">
        @if (r.errorCount > 0) {
          <mat-icon aria-hidden="true">error</mat-icon> {{ 'admin.report.rejected' | transloco: { n: r.errorCount } }}
        } @else if (r.saved) {
          <mat-icon aria-hidden="true">check_circle</mat-icon>
          {{ (r.catalogVersion ? 'admin.report.saved' : 'admin.report.savedNoChange') | transloco: { v: r.catalogVersion } }}
        } @else {
          <mat-icon aria-hidden="true">visibility</mat-icon> {{ 'admin.report.preview' | transloco }}
        }
      </p>
      <dl class="counts">
        <div><dt>{{ 'admin.report.rows' | transloco }}</dt><dd>{{ r.rowsRead }}</dd></div>
        <div><dt>{{ 'admin.report.created' | transloco }}</dt><dd>{{ r.created }}</dd></div>
        <div><dt>{{ 'admin.report.updated' | transloco }}</dt><dd>{{ r.updated }}</dd></div>
        <div><dt>{{ 'admin.report.unchanged' | transloco }}</dt><dd>{{ r.unchanged }}</dd></div>
        <div><dt>{{ 'admin.report.removed' | transloco }}</dt><dd>{{ r.removed }}</dd></div>
      </dl>
      @if (r.issues.length > 0) {
        <table class="liwa-table">
          <caption class="visually-hidden">{{ 'admin.report.issues' | transloco }}</caption>
          <thead>
            <tr>
              <th scope="col">{{ 'admin.report.line' | transloco }}</th>
              <th scope="col">{{ 'admin.report.column' | transloco }}</th>
              <th scope="col">{{ 'admin.report.problem' | transloco }}</th>
            </tr>
          </thead>
          <tbody>
            @for (i of r.issues; track $index) {
              <tr [class.err]="i.severity === 'Error'">
                <td>{{ i.line }}</td>
                <td><code>{{ i.column ?? '' }}</code></td>
                <td>
                  <span class="sev">{{ 'admin.report.' + i.severity | transloco }}</span>
                  {{ i.message }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: `
    .report {
      margin-top: 1rem;
      padding: 0.875rem 1rem;
      border-radius: var(--liwa-radius);
      border-inline-start: 4px solid var(--liwa-success);
      background: var(--liwa-page);
    }
    .report.has-errors {
      border-inline-start-color: var(--liwa-danger);
    }
    .headline {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0 0 0.5rem;
      font-weight: 600;
    }
    .counts {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 1.5rem;
      margin: 0 0 0.5rem;
    }
    .counts dt {
      font-size: 0.75rem;
      color: var(--liwa-text-muted);
    }
    .counts dd {
      margin: 0;
      font-weight: 700;
    }
    tr.err td {
      color: var(--liwa-danger);
    }
    .sev {
      font-weight: 700;
      margin-inline-end: 0.25rem;
    }
  `,
})
export class ImportReportComponent {
  readonly report = input.required<ImportReport>();
}
