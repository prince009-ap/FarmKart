import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerAuction, CustomerAuctionFilter } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-auctions',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatFormFieldModule
  ],
  templateUrl: './customer-auctions.component.html'
})
export class CustomerAuctionsComponent implements OnInit {
  private readonly auctionService = inject(CustomerAuctionService);

  auctions = signal<CustomerAuction[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  // Filters
  searchQuery = signal<string>('');
  selectedCategory = signal<string>('All');
  selectedStatus = signal<string>('All');
  selectedSort = signal<string>('newest');

  readonly categories = ['All', 'Grain', 'Vegetable', 'Fruit', 'Cash Crop', 'Pulses', 'Oilseeds', 'Spices'];
  readonly statusOptions = ['All', 'LIVE', 'UPCOMING', 'ENDED'];
  readonly sortOptions = [
    { label: 'Newest Auctions', value: 'newest' },
    { label: 'Ending Soon', value: 'ending_soon' },
    { label: 'Lowest Starting Price', value: 'price_asc' },
    { label: 'Highest Starting Price', value: 'price_desc' }
  ];

  ngOnInit(): void {
    this.loadAuctions();
  }

  loadAuctions(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const filter: CustomerAuctionFilter = {
      search: this.searchQuery().trim() || undefined,
      category: this.selectedCategory() !== 'All' ? this.selectedCategory() : undefined,
      status: this.selectedStatus() !== 'All' ? this.selectedStatus() : undefined,
      sortBy: this.selectedSort()
    };

    this.auctionService.getMarketplaceAuctions(filter).subscribe({
      next: (data) => {
        this.auctions.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load marketplace auctions. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
    this.loadAuctions();
  }

  onCategoryChange(category: string): void {
    this.selectedCategory.set(category);
    this.loadAuctions();
  }

  onStatusChange(status: string): void {
    this.selectedStatus.set(status);
    this.loadAuctions();
  }

  onSortChange(sortBy: string): void {
    this.selectedSort.set(sortBy);
    this.loadAuctions();
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedCategory.set('All');
    this.selectedStatus.set('All');
    this.selectedSort.set('newest');
    this.loadAuctions();
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
