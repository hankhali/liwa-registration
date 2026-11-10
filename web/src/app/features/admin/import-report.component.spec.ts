import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { setupI18n } from '../../shared/testing';
import { ImportReport } from './admin.service';
import { ImportReportComponent } from './import-report.component';

const base: ImportReport = {
  kind: 'Courses', dryRun: true, rowsRead: 2, created: 1, updated: 0, unchanged: 0, removed: 0, saved: false, catalogVersion: null,
  issues: [], errorCount: 0, warningCount: 0,
};

function render(report: ImportReport, lang: 'en' | 'ar' = 'en'): HTMLElement {
  setupI18n(lang);
  const f = TestBed.createComponent(ImportReportComponent);
  f.componentRef.setInput('report', report);
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('import report', () => {
  it('shows a preview with the counts', () => {
    const el = render(base);
    expect(el.textContent).toContain('Preview only');
    expect(el.querySelector('.counts')!.textContent).toContain('Added');
  });

  it('lists every problem by line and column and says nothing was saved', () => {
    const el = render({
      ...base,
      errorCount: 1,
      issues: [{ severity: 'Error', line: 3, column: 'credits', message: "'99' is too big." }],
    });
    expect(el.textContent).toContain('Nothing was saved: 1 problems');
    const cells = [...el.querySelectorAll('tbody td')].map((td) => td.textContent!.trim());
    expect(cells[0]).toBe('3');
    expect(cells[1]).toBe('credits');
  });

  it('names the catalog version after saving, in Arabic too', () => {
    expect(render({ ...base, dryRun: false, saved: true, catalogVersion: 12 }).textContent).toContain('catalog version 12');
    expect(render({ ...base, dryRun: false, saved: true, catalogVersion: 12 }, 'ar').textContent).toContain('12');
  });
});
