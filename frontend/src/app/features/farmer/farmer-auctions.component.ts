import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, interval } from 'rxjs';
import { takeUntil, switchMap } from 'rxjs/operators';
import { FarmerAuctionService } from './farmer-auction.service';
import { FarmerAuction, FarmerAuctionSummaryCounts } from '../../core/models/farmer-crop.models';
import { AuctionCountdownComponent } from '../../shared/auction-countdown.component';

type AuctionFilter = 'ALL' | 'UPCOMING' | 'LIVE' | 'ENDED' | 'CANCELLED';

@Component({
  selector: 'app-farmer-auctions',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    AuctionCountdownComponent
  ],
  templateUrl: './farmer-auctions.component.html'
})
export class FarmerAuctionsComponent implements OnInit, OnDestroy {
  private readonly auctionService = inject(FarmerAuctionService);
  private readonly destroy$ = new Subject<void>();

  isLoading = signal(true);
  errorMessage = signal<string | null>(null);
  auctions = signal<FarmerAuction[]>([]);
  summaryCounts = signal<FarmerAuctionSummaryCounts>({
    totalAuctions: 0,
    upcomingCount: 0,
    liveCount: 0,
    endedCount: 0,
    cancelledCount: 0
  });

  selectedFilter = signal<AuctionFilter>('ALL');
  searchQuery = signal<string>('');

  filteredAuctions = computed(() => {
    const list = this.auctions();
    const filter = this.selectedFilter();
    const query = this.searchQuery().trim().toLowerCase();

    return list.filter(auction => {
      // Filter by status tab
      const status = (auction.status || '').toUpperCase();
      let matchesFilter = true;
      if (filter === 'UPCOMING') matchesFilter = status === 'SCHEDULED' || status === 'UPCOMING' || status === 'DRAFT';
      else if (filter === 'LIVE') matchesFilter = status === 'LIVE';
      else if (filter === 'ENDED') matchesFilter = status === 'ENDED';
      else if (filter === 'CANCELLED') matchesFilter = status === 'CANCELLED';

      if (!matchesFilter) return false;

      // Filter by search query (crop name or variety)
      if (query) {
        const cropName = (auction.cropName || '').toLowerCase();
        const variety = (auction.variety || '').toLowerCase();
        return cropName.includes(query) || variety.includes(query);
      }

      return true;
    });
  });

  ngOnInit(): void {
    this.loadData();

    // Safe 5-second polling for live auctions
    interval(5000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        const hasLive = this.auctions().some(a => (a.status || '').toUpperCase() === 'LIVE');
        if (hasLive) {
          this.fetchAuctionsSilent();
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

    this.auctionService.getAuctions().subscribe({
      next: (data) => {
        this.auctions.set(data);
        this.updateSummaryFromList(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Unable to load your auctions. Please check your connection and try again.');
        this.isLoading.set(false);
      }
    });
  }

  fetchAuctionsSilent(): void {
    this.auctionService.getAuctions().subscribe({
      next: (data) => {
        this.auctions.set(data);
        this.updateSummaryFromList(data);
      },
      error: () => {}
    });
  }

  setFilter(filter: AuctionFilter): void {
    this.selectedFilter.set(filter);
  }

  onSearchChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchQuery.set(value);
  }

  private updateSummaryFromList(list: FarmerAuction[]): void {
    let total = list.length;
    let upcoming = 0;
    let live = 0;
    let ended = 0;
    let cancelled = 0;

    for (const a of list) {
      const st = (a.status || '').toUpperCase();
      if (st === 'CANCELLED') cancelled++;
      else if (st === 'SCHEDULED' || st === 'UPCOMING' || st === 'DRAFT') upcoming++;
      else if (st === 'LIVE') live++;
      else ended++;
    }

    this.summaryCounts.set({
      totalAuctions: total,
      upcomingCount: upcoming,
      liveCount: live,
      endedCount: ended,
      cancelledCount: cancelled
    });
  }
}
