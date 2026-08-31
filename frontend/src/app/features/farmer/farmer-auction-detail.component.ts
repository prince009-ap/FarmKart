import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, interval } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { FarmerAuctionService } from './farmer-auction.service';
import { FarmerAuction } from '../../core/models/farmer-crop.models';
import { AuctionCountdownComponent } from '../../shared/auction-countdown.component';

@Component({
  selector: 'app-farmer-auction-detail',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    AuctionCountdownComponent
  ],
  templateUrl: './farmer-auction-detail.component.html'
})
export class FarmerAuctionDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly auctionService = inject(FarmerAuctionService);
  private readonly destroy$ = new Subject<void>();

  isLoading = signal(true);
  errorMessage = signal<string | null>(null);
  auction = signal<FarmerAuction | null>(null);
  auctionResult = signal<any | null>(null);
  paymentData = signal<any | null>(null);

  auctionId = signal<string>('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage.set('Invalid auction ID.');
      this.isLoading.set(false);
      return;
    }
    this.auctionId.set(id);
    this.loadAuctionDetails();

    // Poll live details every 5s if auction is LIVE
    interval(5000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        const a = this.auction();
        if (a && (a.status || '').toUpperCase() === 'LIVE') {
          this.fetchSilent();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAuctionDetails(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    const id = this.auctionId();

    this.auctionService.getAuction(id).subscribe({
      next: (data) => {
        this.auction.set(data);
        this.isLoading.set(false);

        if ((data.status || '').toUpperCase() === 'ENDED') {
          this.loadEndedSummaries(id);
        }
      },
      error: (err) => {
        this.errorMessage.set('Unable to load auction details or you do not have permission to view it.');
        this.isLoading.set(false);
      }
    });
  }

  fetchSilent(): void {
    const id = this.auctionId();
    this.auctionService.getAuction(id).subscribe({
      next: (data) => {
        this.auction.set(data);
      },
      error: () => {}
    });
  }

  private loadEndedSummaries(id: string): void {
    this.auctionService.getAuctionResult(id).subscribe({
      next: (res) => this.auctionResult.set(res),
      error: () => {}
    });

    this.auctionService.getAuctionPayment(id).subscribe({
      next: (pay) => this.paymentData.set(pay),
      error: () => {}
    });
  }
}
