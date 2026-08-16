import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MachineryService } from '../../core/services/machinery.service';
import { MachineryReviewService } from '../../core/services/machinery-review.service';
import { MachineryRentalResponse, MachineryRentalStatus } from '../../core/models/machinery.models';
import { MachineryReviewModalComponent, MachineryReviewModalData } from '../machinery/machinery-review-modal.component';

@Component({
  selector: 'app-customer-my-rentals',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDialogModule
  ],
  templateUrl: './customer-my-rentals.component.html'
})
export class CustomerMyRentalsComponent implements OnInit {
  private readonly machineryService = inject(MachineryService);
  private readonly reviewService = inject(MachineryReviewService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  get marketplaceRoute(): string {
    return this.router.url.includes('/farmer/') ? '/farmer/machinery/marketplace' : '/customer/machinery';
  }

  rentals = signal<MachineryRentalResponse[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  selectedTab = signal<'All' | 'Active' | 'Completed' | 'Cancelled'>('All');

  ngOnInit(): void {
    this.loadRentals();
  }

  loadRentals(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.machineryService.getMyRentals().subscribe({
      next: (data) => {
        this.rentals.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load your rentals.');
        this.isLoading.set(false);
      }
    });
  }

  filterRentals(): MachineryRentalResponse[] {
    const list = this.rentals();
    const tab = this.selectedTab();

    if (tab === 'Active') {
      return list.filter(r => ['Booked', 'Confirmed', 'ReadyForHandover', 'RentedOut', 'Returned'].includes(r.rentalStatus));
    }
    if (tab === 'Completed') {
      return list.filter(r => r.rentalStatus === 'Completed');
    }
    if (tab === 'Cancelled') {
      return list.filter(r => r.rentalStatus === 'Cancelled');
    }
    return list;
  }

  onTabChange(tab: 'All' | 'Active' | 'Completed' | 'Cancelled'): void {
    this.selectedTab.set(tab);
  }

  returnRental(rentalId: string): void {
    this.machineryService.updateRentalStatus(rentalId, { newStatus: 'Returned' }).subscribe({
      next: () => {
        this.snackBar.open('Marked as Returned to owner.', 'Close', { duration: 3000 });
        this.loadRentals();
      },
      error: (err) => {
        this.snackBar.open(err?.error?.message || 'Failed to update status.', 'Close', { duration: 4000 });
      }
    });
  }

  cancelRental(rentalId: string): void {
    const reason = prompt('Please enter cancellation reason:');
    if (reason === null) return;

    this.machineryService.updateRentalStatus(rentalId, {
      newStatus: 'Cancelled',
      cancellationReason: reason
    }).subscribe({
      next: () => {
        this.snackBar.open('Rental cancelled successfully.', 'Close', { duration: 3000 });
        this.loadRentals();
      },
      error: (err) => {
        this.snackBar.open(err?.error?.message || 'Failed to cancel rental.', 'Close', { duration: 4000 });
      }
    });
  }

  openReviewModal(rental: MachineryRentalResponse): void {
    this.reviewService.getRentalReview(rental.id).subscribe({
      next: (existingRev) => {
        this.showModal(rental, existingRev);
      },
      error: () => {
        this.showModal(rental, undefined);
      }
    });
  }

  private showModal(rental: MachineryRentalResponse, existingRev?: any): void {
    const dialogRef = this.dialog.open<MachineryReviewModalComponent, MachineryReviewModalData>(
      MachineryReviewModalComponent,
      {
        data: {
          rentalId: rental.id,
          machineryName: rental.machineryName,
          startDate: rental.startDate,
          endDate: rental.endDate,
          existingReview: existingRev
        },
        width: '480px',
        panelClass: 'custom-dialog-container'
      }
    );

    dialogRef.afterClosed().subscribe(res => {
      if (res) {
        this.loadRentals();
      }
    });
  }

  removeHistoryRecord(rentalId: string): void {
    if (!confirm('Remove this completed/cancelled rental record from your history view?')) return;
    this.rentals.update(list => list.filter(r => r.id !== rentalId));
    this.snackBar.open('Rental record removed from history view.', 'Close', { duration: 3000 });
  }

  getStatusBadgeClass(status: MachineryRentalStatus): string {
    switch (status) {
      case 'Booked': return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
      case 'Confirmed': return 'bg-sky-500/20 text-sky-300 border-sky-500/30';
      case 'ReadyForHandover': return 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30';
      case 'RentedOut': return 'bg-blue-500/20 text-blue-300 border-blue-500/30';
      case 'Returned': return 'bg-purple-500/20 text-purple-300 border-purple-500/30';
      case 'Completed': return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30';
      case 'Cancelled': return 'bg-rose-500/20 text-rose-300 border-rose-500/30';
      default: return 'bg-slate-800 text-slate-400 border-slate-700';
    }
  }
}
