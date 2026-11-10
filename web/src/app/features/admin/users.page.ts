import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { formatDateTime } from '../../core/academic/academic.models';
import { LanguageService } from '../../core/i18n/language.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { AdminService, StaffUser, problemTitle } from './admin.service';

/** Staff accounts, passwords and bulk advisor reassignment (brief §11). Students come from the student import. */
@Component({
  selector: 'app-users-page',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, TranslocoPipe, PageHeaderComponent],
  template: `
    <app-page-header [heading]="'admin.users.heading' | transloco" [subtitle]="'admin.users.subtitle' | transloco" />

    @if (message(); as m) {
      <p class="liwa-notice" [class.error]="m.error" role="status">{{ m.text }}</p>
    }

    <section class="liwa-card">
      @if (users.hasValue()) {
        <div class="table-wrap">
          <table class="liwa-table">
            <thead>
              <tr>
                <th scope="col">{{ 'admin.users.name' | transloco }}</th>
                <th scope="col">{{ 'admin.users.role' | transloco }}</th>
                <th scope="col">{{ 'admin.users.advisees' | transloco }}</th>
                <th scope="col">{{ 'admin.users.lastSignIn' | transloco }}</th>
                <th scope="col">{{ 'admin.users.active' | transloco }}</th>
                <th scope="col">{{ 'admin.users.password' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (u of users.value(); track u.id) {
                <tr [class.off]="!u.isActive">
                  <td><strong>{{ u.displayName }}</strong><span class="sub" dir="ltr">{{ u.username }}</span></td>
                  <td>{{ 'roles.' + u.role | transloco }}</td>
                  <td>{{ u.role === 'Advisor' ? u.advisees : '—' }}</td>
                  <td>{{ u.lastSignInAt ? when(u.lastSignInAt) : ('admin.users.never' | transloco) }}</td>
                  <td><mat-slide-toggle [checked]="u.isActive" (change)="toggle(u, $event.checked)" [aria-label]="('admin.users.active' | transloco) + ': ' + u.displayName" /></td>
                  <td>
                    @if (resetting() === u.id) {
                      <span class="inline">
                        <input class="pw" type="password" [(ngModel)]="newPassword" [attr.aria-label]="'admin.users.newPassword' | transloco" autocomplete="new-password" />
                        <button mat-button type="button" (click)="savePassword(u)">{{ 'admin.users.set' | transloco }}</button>
                      </span>
                    } @else {
                      <button mat-button type="button" (click)="resetting.set(u.id); newPassword = ''">{{ 'admin.users.reset' | transloco }}</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>

    <section class="liwa-card" aria-labelledby="new-h">
      <h2 id="new-h">{{ 'admin.users.new' | transloco }}</h2>
      <div class="form">
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>{{ 'admin.users.name' | transloco }}</mat-label><input matInput [(ngModel)]="form.displayName" /></mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>{{ 'login.username' | transloco }}</mat-label><input matInput dir="ltr" [(ngModel)]="form.username" autocomplete="off" /></mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>{{ 'admin.users.email' | transloco }}</mat-label><input matInput dir="ltr" type="email" [(ngModel)]="form.email" /></mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'admin.users.role' | transloco }}</mat-label>
          <mat-select [(ngModel)]="form.role">
            <mat-option value="Advisor">{{ 'roles.Advisor' | transloco }}</mat-option>
            <mat-option value="Admin">{{ 'roles.Admin' | transloco }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>{{ 'login.password' | transloco }}</mat-label><input matInput type="password" [(ngModel)]="form.password" autocomplete="new-password" /><mat-hint>{{ 'admin.users.passwordHint' | transloco }}</mat-hint></mat-form-field>
        <button mat-flat-button type="button" (click)="create()">{{ 'admin.users.create' | transloco }}</button>
      </div>
    </section>

    <section class="liwa-card" aria-labelledby="move-h">
      <h2 id="move-h">{{ 'admin.users.reassign' | transloco }}</h2>
      <p class="liwa-muted">{{ 'admin.users.reassignHint' | transloco }}</p>
      <div class="form">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'admin.users.from' | transloco }}</mat-label>
          <mat-select [(ngModel)]="move.from">
            @for (a of advisors(); track a.id) {<mat-option [value]="a.id">{{ a.displayName }} ({{ a.advisees }})</mat-option>}
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'admin.users.to' | transloco }}</mat-label>
          <mat-select [(ngModel)]="move.to">
            @for (a of advisors(); track a.id) {<mat-option [value]="a.id" [disabled]="!a.isActive">{{ a.displayName }}</mat-option>}
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
          <mat-label>{{ 'admin.users.which' | transloco }}</mat-label>
          <input matInput dir="ltr" [(ngModel)]="move.ids" />
          <mat-hint>{{ 'admin.users.whichHint' | transloco }}</mat-hint>
        </mat-form-field>
        <button mat-flat-button type="button" [disabled]="!move.from || !move.to" (click)="reassign()">{{ 'admin.users.move' | transloco }}</button>
      </div>
    </section>
  `,
  styles: `
    section {
      margin-top: 1rem;
    }
    .table-wrap {
      overflow-x: auto;
    }
    .sub {
      display: block;
      font-size: 0.8125rem;
      color: var(--liwa-text-muted);
    }
    tr.off td {
      color: var(--liwa-text-muted);
    }
    .inline {
      display: inline-flex;
      gap: 0.25rem;
      align-items: center;
    }
    .pw {
      min-height: 36px;
      padding: 0 0.5rem;
      border: 1px solid var(--liwa-border);
      border-radius: 6px;
      background: var(--liwa-surface);
      color: var(--liwa-text);
    }
    .form {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
    }
    .form mat-form-field {
      width: 14rem;
    }
    .form mat-form-field.wide {
      width: 22rem;
    }
  `,
})
export class UsersPage {
  private readonly admin = inject(AdminService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  protected readonly users = resource({ loader: () => this.admin.users() });
  protected readonly advisors = computed(() => (this.users.value() ?? []).filter((u) => u.role === 'Advisor'));
  protected readonly message = signal<{ text: string; error: boolean } | null>(null);
  protected readonly resetting = signal<string | null>(null);
  protected newPassword = '';
  protected form = { displayName: '', username: '', email: '', role: 'Advisor' as 'Advisor' | 'Admin', password: '' };
  protected move = { from: '', to: '', ids: '' };

  protected when(iso: string): string {
    return formatDateTime(iso, this.language.lang());
  }

  private ok(key: string, params?: Record<string, unknown>): void {
    this.message.set({ text: this.transloco.translate(key, params), error: false });
    this.users.reload();
  }

  private fail(e: unknown): void {
    this.message.set({ text: problemTitle(e) ?? this.transloco.translate('admin.users.failed'), error: true });
  }

  protected async toggle(u: StaffUser, active: boolean): Promise<void> {
    try {
      await this.admin.setActive(u.id, active);
      this.ok(active ? 'admin.users.activated' : 'admin.users.deactivated', { name: u.displayName });
    } catch (e) {
      this.fail(e);
      this.users.reload();
    }
  }

  protected async savePassword(u: StaffUser): Promise<void> {
    try {
      await this.admin.setPassword(u.id, this.newPassword);
      this.resetting.set(null);
      this.ok('admin.users.passwordSet', { name: u.displayName });
    } catch (e) {
      this.fail(e);
    }
  }

  protected async create(): Promise<void> {
    try {
      const u = await this.admin.createUser({ ...this.form, email: this.form.email || null });
      this.form = { displayName: '', username: '', email: '', role: 'Advisor', password: '' };
      this.ok('admin.users.created', { name: u.displayName });
    } catch (e) {
      this.fail(e);
    }
  }

  protected async reassign(): Promise<void> {
    try {
      const ids = this.move.ids.split(/[\s,;]+/).filter(Boolean);
      const r = await this.admin.reassign(this.move.from, this.move.to, ids);
      this.ok('admin.users.moved', { n: r.moved });
    } catch (e) {
      this.fail(e);
    }
  }
}
