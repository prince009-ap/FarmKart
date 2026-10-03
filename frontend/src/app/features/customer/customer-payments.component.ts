import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerPaymentHistory } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-payments',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './customer-payments.component.html'
})
export class CustomerPaymentsComponent implements OnInit {
  private readonly auctionService = inject(CustomerAuctionService);

  payments = signal<CustomerPaymentHistory[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadPaymentHistory();
  }

  loadPaymentHistory(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.auctionService.getPaymentHistory().subscribe({
      next: (data) => {
        this.payments.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load payment history. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/images/crop-placeholder.png';
  }
}
