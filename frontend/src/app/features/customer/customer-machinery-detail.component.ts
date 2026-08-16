import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MachineryService } from '../../core/services/machinery.service';
import { MachineryReviewService } from '../../core/services/machinery-review.service';
import { MachineryResponse, MachineryAvailabilityResponse } from '../../core/models/machinery.models';
import { MachineryRatingSummaryResponse } from '../../core/models/machinery-review.models';
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
  private readonly reviewService = inject(MachineryReviewService);
  private readonly snackBar = inject(MatSnackBar);

  machinery = signal<MachineryResponse | null>(null);
  availability = signal<MachineryAvailabilityResponse | null>(null);
  reviewsSummary = signal<MachineryRatingSummaryResponse | null>(null);
  isLoading = signal<boolean>(true);
  isBooking = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  bookingError = signal<string | null>(null);

  selectedImageIndex = signal<number>(0);

  // Booking Form State
  startDate = signal<string>('');
  endDate = signal<string>('');
  driverRequired = signal<boolean>(false);
  paymentMethod = signal<string>('Upi');

  // Calculated Financials
  calculatedDays = signal<number>(0);
  calculatedMachineryAmount = signal<number>(0);
  calculatedDriverAmount = signal<number>(0);
  calculatedTotalAmount = signal<number>(0);
  calculatedTotalPayable = signal<number>(0);

  minStartDate = new Date().toISOString().split('T')[0];

  get backLink(): string {
    return this.router.url.includes('/farmer/') ? '/farmer/machinery/marketplace' : '/customer/machinery';
  }

  get myRentalsRoute(): string {
    return this.router.url.includes('/farmer/') ? '/farmer/my-rentals' : '/customer/my-rentals';
  }

  get farmerProfileRoute(): string {
    const m = this.machinery();
    const prefix = this.router.url.includes('/farmer/') ? '/farmer' : '/customer';
    return `${prefix}/farmers/${m?.ownerUserId}`;
  }

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
        if (m.isOwnedByCurrentUser) {
          this.bookingError.set('You own this machinery listing and cannot rent it.');
        }
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

    this.reviewService.getMachineryReviews(id).subscribe({
      next: (res) => this.reviewsSummary.set(res),
      error: () => {}
    });
  }

  onDriverOptionChanged(required: boolean): void {
    const m = this.machinery();
    if (m && !m.driverAvailable && required) {
      this.driverRequired.set(false);
      return;
    }
    this.driverRequired.set(required);
    this.recalculateFinancials();
  }

  onDatesChanged(): void {
    this.recalculateFinancials();
  }

  recalculateFinancials(): void {
    this.bookingError.set(null);
    const m = this.machinery();

    if (m?.isOwnedByCurrentUser) {
      this.bookingError.set('You own this machinery listing and cannot rent it.');
      this.calculatedDays.set(0);
      return;
    }

    const start = this.startDate();
    const end = this.endDate();

    if (!start || !end || !m) {
      this.calculatedDays.set(0);
      this.calculatedMachineryAmount.set(0);
      this.calculatedDriverAmount.set(0);
      this.calculatedTotalAmount.set(0);
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

    const machineryAmount = days * m.dailyRent;
    const driverAmount = (m.driverAvailable && this.driverRequired()) ? (days * m.driverChargePerDay) : 0;
    const totalAmount = machineryAmount + driverAmount;
    const totalPayable = totalAmount + m.securityDeposit;

    this.calculatedDays.set(days);
    this.calculatedMachineryAmount.set(machineryAmount);
    this.calculatedDriverAmount.set(driverAmount);
    this.calculatedTotalAmount.set(totalAmount);
    this.calculatedTotalPayable.set(totalPayable);
  }

  confirmBooking(): void {
    const m = this.machinery();
    if (!m || !this.startDate() || !this.endDate() || this.calculatedDays() <= 0 || m.isOwnedByCurrentUser) return;

    this.isBooking.set(true);
    this.bookingError.set(null);

    this.machineryService.bookRental(m.id, {
      startDate: this.startDate(),
      endDate: this.endDate(),
      driverRequired: this.driverRequired(),
      paymentMethod: this.paymentMethod()
    }).subscribe({
      next: (res) => {
        this.isBooking.set(false);
        this.snackBar.open('Rental booked successfully!', 'Close', { duration: 4000 });
        this.router.navigate([this.myRentalsRoute]);
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

  getStarArray(rating: number): number[] {
    const full = Math.floor(rating);
    return Array(full).fill(0);
  }
}
