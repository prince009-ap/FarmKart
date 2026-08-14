import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerAuction } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-auction-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './customer-auction-detail.component.html'
})
export class CustomerAuctionDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly auctionService = inject(CustomerAuctionService);

  auction = signal<CustomerAuction | null>(null);
  selectedImageIndex = signal<number>(0);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

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
      },
      error: () => {
        this.errorMessage.set('Auction details could not be found or loaded.');
        this.isLoading.set(false);
      }
    });
  }

  selectImage(index: number): void {
    this.selectedImageIndex.set(index);
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toUpperCase()) {
      case 'LIVE':
        return 'app-badge--active !bg-emerald-100 !text-emerald-800 dark:!bg-emerald-950 dark:!text-emerald-300';
      case 'UPCOMING':
        return 'app-badge--soon !bg-amber-100 !text-amber-800 dark:!bg-amber-950 dark:!text-amber-300';
      case 'ENDED':
        return '!bg-slate-200 !text-slate-700 dark:!bg-slate-800 dark:!text-slate-400';
      default:
        return 'app-badge--soon';
    }
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/images/crop-placeholder.png';
  }
}
