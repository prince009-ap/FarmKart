import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerAuctionService } from './customer-auction.service';
import { AuctionBid, AuctionResult, CustomerAuction } from '../../core/models/customer-auction.models';
import { AuctionCountdownComponent } from '../../shared/auction-countdown.component';

@Component({
  selector: 'app-customer-auction-detail',
  standalone: true,
  imports: [
    TranslatePipe,
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
  private readonly router = inject(Router);
  private readonly auctionService = inject(CustomerAuctionService);

  auction = signal<CustomerAuction | null>(null);
  auctionResult = signal<AuctionResult | null>(null);
  bidsHistory = signal<AuctionBid[]>([]);
  selectedImageIndex = signal<number>(0);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  bidAmountInput = signal<number | null>(null);
  requestedQuantityInput = signal<number | null>(null);
  placingBid = signal<boolean>(false);
  bidError = signal<string | null>(null);
  bidSuccess = signal<string | null>(null);

  getFarmerProfileRoute(farmerUserId: string): string {
    const prefix = this.router.url.includes('/farmer/') ? '/farmer' : '/customer';
    return `${prefix}/farmers/${farmerUserId}`;
  }

  minNextBid = computed(() => {
    const auc = this.auction();
    if (!auc) return 0;
    if (auc.currentHighestBid && auc.currentHighestBid > 0) {
      return auc.currentHighestBid + auc.minimumBidIncrement;
    }
    return auc.startingBidPrice;
  });

  requestedMan = computed(() => {
    const kg = this.requestedQuantityInput() || 0;
    return Math.round((kg / 20) * 100) / 100;
  });

  estimatedTotalPayable = computed(() => {
    const rate = this.bidAmountInput() || 0;
    const man = this.requestedMan();
    return Math.round(rate * man * 100) / 100;
  });

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
        if (!this.requestedQuantityInput() && data.quantityKg) {
          this.requestedQuantityInput.set(data.quantityKg);
        }
        this.isLoading.set(false);
        this.loadBidHistory(id);

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

  loadBidHistory(id: string): void {
    this.auctionService.getAuctionBids(id).subscribe({
      next: (bids) => this.bidsHistory.set(bids),
      error: () => {}
    });
  }

  loadResult(id: string): void {
    this.auctionService.getAuctionResult(id).subscribe({
      next: (res) => this.auctionResult.set(res),
      error: () => {}
    });
  }

  placeBid(): void {
    const auc = this.auction();
    const amount = this.bidAmountInput();
    const reqKg = this.requestedQuantityInput();
    this.bidError.set(null);
    this.bidSuccess.set(null);

    if (!auc) return;
    if (!amount || amount <= 0) {
      this.bidError.set('Please enter a valid bid amount greater than zero.');
      return;
    }

    if (!reqKg || reqKg <= 0) {
      this.bidError.set('Please enter a valid required quantity in Kg greater than zero.');
      return;
    }

    if (reqKg > auc.quantityKg) {
      this.bidError.set(`Requested quantity (${reqKg} Kg) cannot exceed total auction quantity (${auc.quantityKg} Kg).`);
      return;
    }

    const minNeeded = this.minNextBid();
    if (amount < minNeeded) {
      this.bidError.set(`Minimum next bid rate must be at least ₹${minNeeded} / Man.`);
      return;
    }

    this.placingBid.set(true);
    this.auctionService.placeBid(auc.id, amount, reqKg).subscribe({
      next: (newBid) => {
        this.bidSuccess.set(`Bid of ₹${newBid.amount} / Man for ${newBid.requestedQuantityKg} Kg (${newBid.requestedQuantityMan} Man) placed successfully!`);
        this.bidAmountInput.set(null);
        this.placingBid.set(false);
        this.loadAuction(auc.id);
      },
      error: (err) => {
        this.bidError.set(err?.error?.message || 'Unable to place bid. Please try again.');
        this.placingBid.set(false);
      }
    });
  }

  selectImage(index: number): void {
    this.selectedImageIndex.set(index);
  }

  isAuctionEnded(): boolean {
    const auc = this.auction();
    if (!auc) return false;
    const now = Date.now();
    const end = new Date(auc.endTimeUtc).getTime();
    return auc.status === 'ENDED' || now >= end;
  }

  isAuctionLive(): boolean {
    const auc = this.auction();
    if (!auc) return false;
    if (this.isAuctionEnded()) return false;
    const now = Date.now();
    const start = new Date(auc.startTimeUtc).getTime();
    const end = new Date(auc.endTimeUtc).getTime();
    return auc.status === 'LIVE' || auc.status === 'Live' || (now >= start && now < end);
  }

  goToCheckout(): void {
    const auc = this.auction();
    if (auc) {
      this.router.navigate(['/customer/auctions', auc.id, 'checkout']);
    }
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/images/crop-placeholder.png';
  }
}
