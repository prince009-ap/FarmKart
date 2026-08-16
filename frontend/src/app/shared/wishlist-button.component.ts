import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { WishlistService } from '../core/services/wishlist.service';
import { WishlistItemType } from '../core/models/wishlist.models';

@Component({
  selector: 'app-wishlist-button',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatSnackBarModule],
  template: `
    <button
      mat-icon-button
      type="button"
      [title]="isEnded ? 'Ended auctions cannot be added to wishlist' : (isFavorited() ? 'Remove from Wishlist' : 'Add to Wishlist')"
      (click)="toggleWishlist($event)"
      [disabled]="isProcessing()"
      [class.opacity-50]="isEnded"
      class="transition-all transform active:scale-95 text-rose-500 hover:text-rose-600 focus:outline-none"
    >
      <mat-icon [class.fill-rose-500]="isFavorited()">
        {{ isFavorited() ? 'favorite' : 'favorite_border' }}
      </mat-icon>
    </button>
  `,
  styles: [`
    .fill-rose-500 {
      font-weight: bold;
    }
  `]
})
export class WishlistButtonComponent {
  @Input({ required: true }) itemType!: WishlistItemType;
  @Input({ required: true }) itemId!: string;
  @Input() isEnded = false;

  @Input()
  set isFavoritedInitial(val: boolean) {
    this.isFavorited.set(val);
  }

  @Output() favoritedChange = new EventEmitter<boolean>();

  isFavorited = signal<boolean>(false);
  isProcessing = signal<boolean>(false);

  private readonly wishlistService = inject(WishlistService);
  private readonly snackBar = inject(MatSnackBar);

  toggleWishlist(event: MouseEvent): void {
    event.stopPropagation();
    event.preventDefault();

    if (this.isProcessing()) return;

    if (this.isEnded) {
      this.snackBar.open('Ended auctions cannot be added to wishlist.', 'Close', {
        duration: 3500,
        panelClass: ['bg-amber-900', 'text-white']
      });
      return;
    }

    const current = this.isFavorited();
    const nextState = !current;

    // Optimistic UI update
    this.isFavorited.set(nextState);
    this.isProcessing.set(true);

    if (current) {
      // Remove
      this.wishlistService.removeItem(this.itemType, this.itemId).subscribe({
        next: () => {
          this.isProcessing.set(false);
          this.favoritedChange.emit(false);
          this.snackBar.open('Removed from wishlist', 'Close', { duration: 2000 });
        },
        error: (err) => {
          // Revert on error
          this.isFavorited.set(current);
          this.isProcessing.set(false);
          const msg = err.error?.message || 'Failed to remove from wishlist.';
          this.snackBar.open(msg, 'Close', { duration: 3000 });
        }
      });
    } else {
      // Add
      this.wishlistService.addItem({ itemType: this.itemType, itemId: this.itemId }).subscribe({
        next: () => {
          this.isProcessing.set(false);
          this.favoritedChange.emit(true);
          this.snackBar.open('Added to wishlist ❤️', 'Close', { duration: 2000 });
        },
        error: (err) => {
          // Revert on error
          this.isFavorited.set(current);
          this.isProcessing.set(false);
          const msg = err.error?.message || 'Ended auctions cannot be added to wishlist.';
          this.snackBar.open(msg, 'Close', { duration: 3500 });
        }
      });
    }
  }
}
