import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerAuction, CustomerAuctionFilter } from '../../core/models/customer-auction.models';
import { AuctionCountdownComponent } from '../../shared/auction-countdown.component';
import { WishlistButtonComponent } from '../../shared/wishlist-button.component';

@Component({
  selector: 'app-customer-auctions',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatFormFieldModule,
    MatCheckboxModule,
    AuctionCountdownComponent,
    WishlistButtonComponent
  ],
  templateUrl: './customer-auctions.component.html'
})
export class CustomerAuctionsComponent implements OnInit, OnDestroy {
  private readonly auctionService = inject(CustomerAuctionService);
  private readonly router = inject(Router);
  private searchSubject = new Subject<string>();
  private searchSub?: Subscription;

  getFarmerProfileRoute(farmerUserId: string): string {
    const prefix = this.router.url.includes('/farmer/') ? '/farmer' : '/customer';
    return `${prefix}/farmers/${farmerUserId}`;
  }

  auctions = signal<CustomerAuction[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  // Filters
  searchQuery = signal<string>('');
  selectedCategory = signal<string>('All');
  selectedStatus = signal<string>('All');
  selectedSort = signal<string>('newest');
  minPrice = signal<number | null>(null);
  maxPrice = signal<number | null>(null);
  minQty = signal<number | null>(null);
  maxQty = signal<number | null>(null);
  endingSoon = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(12);
  totalPages = signal<number>(1);
  totalCount = signal<number>(0);

  readonly categories = ['All', 'Grain', 'Cereal', 'Vegetable', 'Fruit', 'Cash Crop', 'Pulses', 'Oilseeds', 'Oilseed', 'Spices', 'Fodder', 'Other'];
  readonly statusOptions = ['All', 'LIVE', 'UPCOMING', 'ENDED', 'ENDING_SOON'];
  readonly sortOptions = [
    { label: 'Newest Auctions', value: 'newest' },
    { label: 'Ending Soon', value: 'ending_soon' },
    { label: 'Lowest Starting Price', value: 'price_asc' },
    { label: 'Highest Starting Price', value: 'price_desc' },
    { label: 'Highest Current Bid', value: 'highest_bid' },
    { label: 'Oldest Auctions', value: 'oldest' }
  ];

  ngOnInit(): void {
    // 300ms debounce on search input
    this.searchSub = this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe((query) => {
        this.searchQuery.set(query);
        this.currentPage.set(1);
        this.loadAuctions();
      });

    this.loadAuctions();
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  loadAuctions(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const filter: CustomerAuctionFilter = {
      search: this.searchQuery().trim() || undefined,
      category: this.selectedCategory() !== 'All' ? this.selectedCategory() : undefined,
      status: this.selectedStatus() !== 'All' ? this.selectedStatus() : undefined,
      sortBy: this.selectedSort(),
      minPricePerMan: this.minPrice() !== null && this.minPrice()! > 0 ? this.minPrice()! : undefined,
      maxPricePerMan: this.maxPrice() !== null && this.maxPrice()! > 0 ? this.maxPrice()! : undefined,
      minQuantityKg: this.minQty() !== null && this.minQty()! > 0 ? this.minQty()! : undefined,
      maxQuantityKg: this.maxQty() !== null && this.maxQty()! > 0 ? this.maxQty()! : undefined,
      endingSoon: this.endingSoon() ? true : undefined,
      page: this.currentPage(),
      pageSize: this.pageSize()
    };

    this.auctionService.getMarketplaceAuctions(filter).subscribe({
      next: (res) => {
        this.auctions.set(res.items);
        this.totalCount.set(res.totalCount);
        this.totalPages.set(res.totalPages);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load marketplace auctions. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  onSearchInput(value: string): void {
    this.searchSubject.next(value);
  }

  onCategoryChange(category: string): void {
    this.selectedCategory.set(category);
    this.currentPage.set(1);
    this.loadAuctions();
  }

  onStatusChange(status: string): void {
    this.selectedStatus.set(status);
    this.currentPage.set(1);
    this.loadAuctions();
  }

  onSortChange(sortBy: string): void {
    this.selectedSort.set(sortBy);
    this.currentPage.set(1);
    this.loadAuctions();
  }

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadAuctions();
  }

  toggleEndingSoon(): void {
    this.endingSoon.set(!this.endingSoon());
    this.currentPage.set(1);
    this.loadAuctions();
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedCategory.set('All');
    this.selectedStatus.set('All');
    this.selectedSort.set('newest');
    this.minPrice.set(null);
    this.maxPrice.set(null);
    this.minQty.set(null);
    this.maxQty.set(null);
    this.endingSoon.set(false);
    this.currentPage.set(1);
    this.loadAuctions();
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
      this.loadAuctions();
    }
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
