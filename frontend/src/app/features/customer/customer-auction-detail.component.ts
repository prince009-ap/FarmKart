import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerAuctionService } from './customer-auction.service';
import { AuctionResult, CustomerAuction } from '../../core/models/customer-auction.models';
import { AuctionCountdownComponent } from '../../shared/auction-countdown.component';

@Component({
  selector: 'app-customer-auction-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    AuctionCountdownComponent
  ],
  templateUrl: './customer-auction-detail.component.html'
})
export class CustomerAuctionDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly auctionService = inject(CustomerAuctionService);

  auction = signal<CustomerAuction | null>(null);
  auctionResult = signal<AuctionResult | null>(null);
  selectedImageIndex = signal<number>(0);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  bidAmount = signal<number | null>(null);
  isSubmittingBid = signal<boolean>(false);
  bidSuccessMessage = signal<string | null>(null);
  bidErrorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadAuction(id);
    } else {
      this.errorMessage.set('Invalid auction ID.');
      this.isLoading.set(false);
    }
  }

  loadAuction(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.auctionService.getAuctionById(id).subscribe({
      next: (data) => {
        this.auction.set(data);
        this.isLoading.set(false);

        // Pre-fill minimum valid bid
        const nextMin = data.currentHighestBid > 0
          ? data.currentHighestBid + data.minimumBidIncrement
          : data.startingBidPrice;
        this.bidAmount.set(nextMin);

        // Load result if ended
        if (data.status === 'ENDED' || new Date(data.endTimeUtc).getTime() <= Date.now()) {
          this.loadResult(id);
        }
      },
      error: () => {
        this.errorMessage.set('Auction details could not be found or loaded.');
        this.isLoading.set(false);
      }
    });
  }

  loadResult(id: string): void {
    this.auctionService.getAuctionResult(id).subscribe({
      next: (res) => this.auctionResult.set(res),
      error: () => {}
    });
  }

  selectImage(index: number): void {
    this.selectedImageIndex.set(index);
  }

  isAuctionLive(): boolean {
    const auc = this.auction();
    if (!auc) return false;
    const now = Date.now();
    const end = new Date(auc.endTimeUtc).getTime();
    return auc.status === 'LIVE' && now < end;
  }

  isAuctionEnded(): boolean {
    const auc = this.auction();
    if (!auc) return false;
    const now = Date.now();
    const end = new Date(auc.endTimeUtc).getTime();
    return auc.status === 'ENDED' || now >= end;
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/images/crop-placeholder.png';
  }
}
