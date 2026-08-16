import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MachineryService } from '../../core/services/machinery.service';
import { MachineryResponse, MachineryAvailabilityResponse } from '../../core/models/machinery.models';
import { WishlistButtonComponent } from '../../shared/wishlist-button.component';

@Component({
  selector: 'app-customer-machinery-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    WishlistButtonComponent
  ],
  templateUrl: './customer-machinery-detail.component.html'
})
export class CustomerMachineryDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly machineryService = inject(MachineryService);
  private readonly snackBar = inject(MatSnackBar);

  machinery = signal<MachineryResponse | null>(null);
  availability = signal<MachineryAvailabilityResponse | null>(null);
  isLoading = signal<boolean>(true);
  isBooking = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  bookingError = signal<string | null>(null);

  selectedImageIndex = signal<number>(0);

  // Booking Form State
  startDate = signal<string>('');
  endDate = signal<string>('');
  paymentMethod = signal<string>('Upi');

  // Calculated Financials
  calculatedDays = signal<number>(0);
  calculatedTotalRent = signal<number>(0);
  calculatedTotalPayable = signal<number>(0);

  minStartDate = new Date().toISOString().split('T')[0];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage.set('Invalid machinery ID.');
      this.isLoading.set(false);
      return;
    }
    this.loadDetail(id);
  }

  loadDetail(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.machineryService.getMachineryById(id).subscribe({
      next: (m) => {
        this.machinery.set(m);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Machinery not found or unavailable.');
        this.isLoading.set(false);
      }
    });

    this.machineryService.getAvailability(id).subscribe({
      next: (a) => this.availability.set(a),
      error: () => {}
    });
  }

  onDatesChanged(): void {
    this.bookingError.set(null);
    const start = this.startDate();
    const end = this.endDate();
    const m = this.machinery();

    if (!start || !end || !m) {
      this.calculatedDays.set(0);
      this.calculatedTotalRent.set(0);
      this.calculatedTotalPayable.set(0);
      return;
    }

    const sDate = new Date(start);
    const eDate = new Date(end);

    if (eDate < sDate) {
      this.bookingError.set('End date must be on or after start date.');
      this.calculatedDays.set(0);
      return;
    }

    // Check date overlap against booked ranges
    const isOverlapping = this.availability()?.bookedRanges.some(r => {
      const rStart = new Date(r.startDate);
      const rEnd = new Date(r.endDate);
      return sDate <= rEnd && eDate >= rStart;
    });

    if (isOverlapping) {
      this.bookingError.set('The selected date range overlaps with an existing booking.');
      this.calculatedDays.set(0);
      return;
    }

    const diffTime = Math.abs(eDate.getTime() - sDate.getTime());
    const days = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;

    const totalRent = days * m.dailyRent;
    const totalPayable = totalRent + m.securityDeposit;

    this.calculatedDays.set(days);
    this.calculatedTotalRent.set(totalRent);
    this.calculatedTotalPayable.set(totalPayable);
  }

  confirmBooking(): void {
    const m = this.machinery();
    if (!m || !this.startDate() || !this.endDate() || this.calculatedDays() <= 0) return;

    this.isBooking.set(true);
    this.bookingError.set(null);

    this.machineryService.bookRental(m.id, {
      startDate: this.startDate(),
      endDate: this.endDate(),
      paymentMethod: this.paymentMethod()
    }).subscribe({
      next: (res) => {
        this.isBooking.set(false);
        this.snackBar.open('Rental booked successfully!', 'Close', { duration: 4000 });
        this.router.navigate(['/customer/my-rentals']);
      },
      error: (err) => {
        this.isBooking.set(false);
        this.bookingError.set(err?.error?.message || 'Failed to process rental booking.');
      }
    });
  }

  selectImage(index: number): void {
    this.selectedImageIndex.set(index);
  }
}
