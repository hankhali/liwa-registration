import { Component, Injectable, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';

export interface ConfirmData {
  title: string;
  body?: string;
  confirm: string;
  cancel: string;
  /** Red confirm button for things that can't be undone. */
  danger?: boolean;
}

/** A yes/no question in a keyboard-accessible dialog (focus is trapped and returned by Material). */
@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    @if (data.body) {
      <mat-dialog-content><p>{{ data.body }}</p></mat-dialog-content>
    }
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="false">{{ data.cancel }}</button>
      <button mat-flat-button type="button" [class.danger]="data.danger" [mat-dialog-close]="true" cdkFocusInitial>{{ data.confirm }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .danger {
      --mat-button-filled-container-color: var(--liwa-danger);
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  /** Resolves true when the person confirms. */
  async ask(data: ConfirmData): Promise<boolean> {
    const ref = this.dialog.open(ConfirmDialogComponent, { data, autoFocus: 'dialog', restoreFocus: true, maxWidth: '480px' });
    return (await firstValueFrom(ref.afterClosed())) === true;
  }
}
