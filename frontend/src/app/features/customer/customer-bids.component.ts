import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerMyBid } from '../../core/models/customer-auction.models';
import { AuctionCountdownComponent } from '../../shared/auction-countdown.component';

@Component({
  selector: 'app-customer-bids',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    AuctionCountdownComponent
  ],
  templateUrl: './customer-bids.component.html'
})
export class CustomerBidsComponent implements OnInit {
  private readonly auctionService = inject(CustomerAuctionService);

  bids = signal<CustomerMyBid[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadMyBids();
  }

  loadMyBids(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.auctionService.getMyBids().subscribe({
      next: (data) => {
        this.bids.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load your bids. Please try again later.');
        this.isLoading.set(false);
      }
    });
  }

  getBidStatusBadgeClass(bidStatus: string, auctionStatus: string, allocStatus?: string | null): string {
    const status = allocStatus || bidStatus;
    if (auctionStatus === 'ENDED') {
      if (status === 'WON' || status === 'HIGHEST BID') {
        return '!bg-emerald-100 !text-emerald-800 dark:!bg-emerald-950 dark:!text-emerald-300 border-emerald-300';
      }
      if (status === 'PARTIALLY_WON') {
        return '!bg-amber-100 !text-amber-800 dark:!bg-amber-950 dark:!text-amber-300 border-amber-300';
      }
      return '!bg-rose-100 !text-rose-800 dark:!bg-rose-950 dark:!text-rose-300 border-rose-300';
    }

    if (status === 'HIGHEST BID') {
      return '!bg-emerald-100 !text-emerald-800 dark:!bg-emerald-950 dark:!text-emerald-300 border-emerald-300';
    }
    return '!bg-amber-100 !text-amber-800 dark:!bg-amber-950 dark:!text-amber-300 border-amber-300';
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/images/crop-placeholder.png';
  }
}
