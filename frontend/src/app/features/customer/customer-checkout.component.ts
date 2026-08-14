import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatRadioModule } from '@angular/material/radio';
import { CustomerAuctionService } from './customer-auction.service';
import { AuctionPayment, AuctionResult, CustomerAuction } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-checkout',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatRadioModule
  ],
  templateUrl: './customer-checkout.component.html'
})
export class CustomerCheckoutComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auctionService = inject(CustomerAuctionService);

  auction = signal<CustomerAuction | null>(null);
  auctionResult = signal<AuctionResult | null>(null);
  completedPayment = signal<AuctionPayment | null>(null);

  selectedPaymentMethod = signal<string>('UPI');
  isLoading = signal<boolean>(true);
  isProcessingPayment = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  paymentError = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadCheckoutDetails(id);
    } else {
      this.errorMessage.set('Invalid auction ID for checkout.');
      this.isLoading.set(false);
    }
  }

  loadCheckoutDetails(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.auctionService.getAuctionById(id).subscribe({
      next: (auc) => {
        this.auction.set(auc);

        this.auctionService.getAuctionResult(id).subscribe({
          next: (res) => {
            this.auctionResult.set(res);
            this.isLoading.set(false);

            if (res.customerResultStatus !== 'WON') {
              this.errorMessage.set('Only the winning customer can proceed to payment for this auction.');
            }
          },
          error: () => {
            this.errorMessage.set('Failed to verify auction winner status.');
            this.isLoading.set(false);
          }
        });
      },
      error: () => {
        this.errorMessage.set('Auction details could not be found.');
        this.isLoading.set(false);
      }
    });
  }

  calculateTotalPayable(): number {
    const auc = this.auction();
    const res = this.auctionResult();
    if (!auc || !res || !res.winningBidAmount) return 0;
    return auc.quantity * res.winningBidAmount;
  }

  payNow(): void {
    const auc = this.auction();
    if (!auc) return;

    this.isProcessingPayment.set(true);
    this.paymentError.set(null);

    this.auctionService.processAuctionPayment(auc.id, this.selectedPaymentMethod()).subscribe({
      next: (payment) => {
        this.isProcessingPayment.set(false);
        this.completedPayment.set(payment);
      },
      error: (err) => {
        this.isProcessingPayment.set(false);
        this.paymentError.set(err?.error?.message || 'Payment processing failed. Please try again.');
      }
    });
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/images/crop-placeholder.png';
  }
}
