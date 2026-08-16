import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MachineryService } from '../../core/services/machinery.service';
import { MachineryResponse } from '../../core/models/machinery.models';

@Component({
  selector: 'app-my-machinery',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './my-machinery.component.html'
})
export class MyMachineryComponent implements OnInit {
  private readonly machineryService = inject(MachineryService);
  private readonly snackBar = inject(MatSnackBar);

  machineryList = signal<MachineryResponse[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadMyMachinery();
  }

  loadMyMachinery(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.machineryService.getMyMachinery().subscribe({
      next: (data) => {
        this.machineryList.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load your machinery listings.');
        this.isLoading.set(false);
      }
    });
  }

  deleteMachinery(id: string, name: string): void {
    if (!confirm(`Are you sure you want to delete listing '${name}'?`)) return;

    this.machineryService.deleteMachinery(id).subscribe({
      next: () => {
        this.snackBar.open('Machinery deleted successfully.', 'Close', { duration: 3000 });
        this.loadMyMachinery();
      },
      error: () => {
        this.snackBar.open('Failed to delete machinery.', 'Close', { duration: 3000 });
      }
    });
  }
}
