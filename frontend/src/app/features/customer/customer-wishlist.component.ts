import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WishlistService } from '../../core/services/wishlist.service';
import { WishlistCountResponse, WishlistItemResponse, WishlistItemType } from '../../core/models/wishlist.models';
import { WishlistButtonComponent } from '../../shared/wishlist-button.component';

@Component({
  selector: 'app-customer-wishlist',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    WishlistButtonComponent
  ],
  templateUrl: './customer-wishlist.component.html'
})
export class CustomerWishlistComponent implements OnInit {
  items = signal<WishlistItemResponse[]>([]);
  counts = signal<WishlistCountResponse | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  selectedTab = signal<'All' | 'Crop' | 'Auction' | 'Machinery'>('All');

  constructor(private wishlistService: WishlistService) {}

  ngOnInit(): void {
    this.loadWishlist();
  }

  loadWishlist(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const type = this.selectedTab() === 'All' ? undefined : (this.selectedTab() as WishlistItemType);

    this.wishlistService.getWishlist(type).subscribe({
      next: (data) => {
        this.items.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load your wishlist.');
        this.isLoading.set(false);
      }
    });

    this.wishlistService.getCount().subscribe({
      next: (cnt) => this.counts.set(cnt),
      error: () => {}
    });
  }

  onTabChange(tab: 'All' | 'Crop' | 'Auction' | 'Machinery'): void {
    this.selectedTab.set(tab);
    this.loadWishlist();
  }

  onItemRemoved(itemId: string): void {
    this.items.set(this.items().filter(i => i.itemId !== itemId));
    this.loadWishlist();
  }
}
