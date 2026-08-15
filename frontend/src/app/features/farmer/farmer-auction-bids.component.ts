import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, interval } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { FarmerAuctionService } from './farmer-auction.service';
import { FarmerAuction, FarmerAuctionBid } from '../../core/models/farmer-crop.models';

@Component({
  selector: 'app-farmer-auction-bids',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './farmer-auction-bids.component.html'
})
export class FarmerAuctionBidsComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly auctionService = inject(FarmerAuctionService);
  private readonly destroy$ = new Subject<void>();

  isLoading = signal(true);
  errorMessage = signal<string | null>(null);
  auction = signal<FarmerAuction | null>(null);
  bids = signal<FarmerAuctionBid[]>([]);

  auctionId = signal<string>('');
  selectedSort = signal<string>('highest_bid');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage.set('Invalid auction ID.');
      this.isLoading.set(false);
      return;
    }
    this.auctionId.set(id);
    this.loadData();

    // Poll live bids every 5 seconds
    interval(5000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        const a = this.auction();
        if (a && (a.status || '').toUpperCase() === 'LIVE') {
          this.fetchBidsSilent();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    const id = this.auctionId();
    const sort = this.selectedSort();

    this.auctionService.getAuction(id).subscribe({
      next: (auctionData) => {
        this.auction.set(auctionData);
        this.fetchBids(id, sort);
      },
      error: () => {
        this.errorMessage.set('Unable to load auction info.');
        this.isLoading.set(false);
      }
    });
  }

  fetchBids(id: string, sort: string): void {
    this.auctionService.getAuctionBids(id, sort).subscribe({
      next: (bidsData) => {
        this.bids.set(bidsData);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Unable to load bidding activity.');
        this.isLoading.set(false);
      }
    });
  }

  fetchBidsSilent(): void {
    const id = this.auctionId();
    const sort = this.selectedSort();
    this.auctionService.getAuctionBids(id, sort).subscribe({
      next: (bidsData) => this.bids.set(bidsData),
      error: () => {}
    });
  }

  onSortChange(newSort: string): void {
    this.selectedSort.set(newSort);
    this.fetchBids(this.auctionId(), newSort);
  }
}
