import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-forbidden',
  imports: [MatButtonModule, RouterLink, TranslocoPipe],
  template: `
    <section class="liwa-card message">
      <h1>{{ 'errors.forbidden.heading' | transloco }}</h1>
      <p>{{ 'errors.forbidden.body' | transloco }}</p>
      <a mat-flat-button [routerLink]="auth.homeUrl()">{{ 'errors.backHome' | transloco }}</a>
    </section>
  `,
  styles: `.message { max-width: 560px; margin: 2rem auto; text-align: center; }`,
})
export class ForbiddenPage {
  protected readonly auth = inject(AuthService);
}
