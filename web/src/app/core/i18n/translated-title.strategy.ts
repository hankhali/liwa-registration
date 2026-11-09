import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { Subscription } from 'rxjs';

/** Route `title` values are translation keys; the document title follows the active language. */
@Injectable({ providedIn: 'root' })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private sub?: Subscription;

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const key = this.buildTitle(snapshot);
    this.sub?.unsubscribe();
    this.sub = this.transloco
      .selectTranslate(['app.name', key ?? 'app.name'])
      .subscribe(([app, page]: string[]) => this.title.setTitle(key ? `${page} · ${app}` : app));
  }
}
