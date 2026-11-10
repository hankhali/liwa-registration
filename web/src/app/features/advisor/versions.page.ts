import { Component, inject, resource } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { AdminService } from '../admin/admin.service';

/** Catalog version history, visible to advisors (brief §11): what changed in the data, when and by whom. */
@Component({
  selector: 'app-versions-page',
  imports: [TranslocoPipe, PageHeaderComponent],
  template: `
    <app-page-header [heading]="'admin.versions.heading' | transloco" [subtitle]="'admin.versions.subtitle' | transloco" />
    @if (versions.hasValue()) {
      <section class="liwa-card table-wrap">
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
                <td>{{ 'admin.kinds.' + v.kind | transloco }}</td>
                <td>{{ 'admin.versions.sources.' + v.source | transloco }}</td>
                <td>{{ v.actorName ?? ('history.system' | transloco) }}</td>
                <td>{{ 'admin.versions.counts' | transloco: v }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    } @else {
      <p class="liwa-muted">{{ 'common.loading' | transloco }}</p>
    }
  `,
  styles: `
    .table-wrap {
      overflow-x: auto;
    }
  `,
})
export class VersionsPage {
  private readonly admin = inject(AdminService);
  private readonly language = inject(LanguageService);
  protected readonly versions = resource({ loader: () => this.admin.versions() });

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }
}
