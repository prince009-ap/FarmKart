import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly dialog = inject(MatDialog);

  confirm(data: ConfirmDialogData): Observable<boolean> {
    return this.dialog
      .open(ConfirmDialogComponent, {
        width: '420px',
        maxWidth: '95vw',
        panelClass: 'fk-confirm-dialog-panel',
        autoFocus: 'dialog',
        data
      })
      .afterClosed()
      .pipe(map(result => !!result));
  }
}
