import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FarmerCropService } from './farmer-crop.service';
import { FarmerAuctionService } from './farmer-auction.service';
import { CreateFarmerAuctionRequest, CropImage, CropStockSummary, CropStockTransaction, FarmerAuction, FarmerCrop } from '../../core/models/farmer-crop.models';
import { AuctionCountdownComponent } from '../../shared/auction-countdown.component';

@Component({
  selector: 'app-farmer-crop-detail',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AuctionCountdownComponent
  ],
  templateUrl: './farmer-crop-detail.component.html'
})
export class FarmerCropDetailComponent implements OnInit {
  private readonly cropService = inject(FarmerCropService);
  private readonly auctionService = inject(FarmerAuctionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  crop = signal<FarmerCrop | null>(null);
  selectedImage = signal<string | null>(null);
  imageError = signal<boolean>(false);

  showDeleteModal = signal<boolean>(false);
  deleting = signal<boolean>(false);

  // Stock Management Signals
  stockSummary = signal<CropStockSummary | null>(null);
  stockHistory = signal<CropStockTransaction[]>([]);
  showStockModal = signal<boolean>(false);
  showHistoryModal = signal<boolean>(false);
  savingStock = signal<boolean>(false);
  loadingHistory = signal<boolean>(false);
  stockErrorMessage = signal<string | null>(null);

  // Stock Form Inputs
  stockQuantity = signal<number | null>(null);
  stockUnit = signal<string>('Kilogram');
  stockTransactionType = signal<string>('Harvest');
  stockNotes = signal<string>('');
  auctions = signal<FarmerAuction[]>([]);
  showAuctionModal = signal(false);
  savingAuction = signal(false);
  auctionError = signal<string | null>(null);
  auctionQuantity = signal<number | null>(null);
  auctionUnit = signal('Kilogram');
  startingBidPrice = signal<number | null>(null);
  minimumBidIncrement = signal<number | null>(null);
  auctionStart = signal('');
  auctionDuration = signal('1 Day');

  readonly DURATION_OPTIONS = [
    { label: '5 Hours', value: '5 Hours' },
    { label: '12 Hours', value: '12 Hours' },
    { label: '1 Day (24 Hours)', value: '1 Day' },
    { label: '3 Days', value: '3 Days' },
    { label: '7 Days', value: '7 Days' },
    { label: 'Custom Hours...', value: 'Custom' }
  ];
  customDurationHours = signal<number | null>(null);
  isCustomDuration = computed(() => this.auctionDuration() === 'Custom');

  previewEndTime = computed(() => {
    const start = this.auctionStart();
    if (!start) return null;
    const d = this.auctionDuration();
    let hours: number;
    if (d === 'Custom') {
      hours = this.customDurationHours() ?? 0;
      if (hours <= 0) return null;
    } else {
      const map: Record<string, number> = { '5 Hours': 5, '12 Hours': 12, '1 Day': 24, '3 Days': 72, '7 Days': 168 };
      hours = map[d] ?? 0;
    }
    const dt = new Date(start);
    dt.setTime(dt.getTime() + hours * 3600 * 1000);
    return dt;
  });

  readonly availableStockUnits = [
    { label: 'Kilogram (Kg)', value: 'Kilogram' },
    { label: 'Quintal (100 Kg)', value: 'Quintal' },
    { label: 'Ton (1000 Kg)', value: 'Ton' }
  ];

  readonly transactionTypes = [
    { label: 'Harvest Record', value: 'Harvest' },
    { label: 'Stock Adjustment', value: 'Adjustment' },
    { label: 'Quantity Correction', value: 'Correction' }
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadCrop(id);
    }
  }

  loadCrop(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.cropService.getCropById(id).subscribe({
      next: (data) => {
        this.crop.set(data);
        const primary = data.primaryImageUrl || (data.images && data.images.length > 0 ? data.images[0].imageUrl : null);
        this.selectedImage.set(primary);
        this.imageError.set(false);
        this.loading.set(false);

        // Load Stock Summary if harvest eligible
        this.loadStockSummary(data.id);
        this.loadAuctions();
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Unable to load crop details.');
        this.loading.set(false);
      }
    });
  }

  loadAuctions(): void { 
    const currentCropId = this.crop()?.id;
    if (!currentCropId) return;
    this.auctionService.getAuctions().subscribe({ 
      next: auctions => this.auctions.set(auctions.filter(a => a.cropId && a.cropId.toLowerCase() === currentCropId.toLowerCase())), 
      error: () => this.auctions.set([]) 
    }); 
  }
  openAuctionModal(): void { this.auctionError.set(null); this.auctionQuantity.set(null); this.startingBidPrice.set(null); this.minimumBidIncrement.set(null); this.auctionDuration.set('1 Day'); this.customDurationHours.set(null); this.auctionStart.set(''); this.showAuctionModal.set(true); }
  closeAuctionModal(): void { this.showAuctionModal.set(false); }
  createAuction(): void {
    const crop = this.crop(); const quantity = this.auctionQuantity(); const price = this.startingBidPrice(); const increment = this.minimumBidIncrement();
    let duration = this.auctionDuration();
    if (duration === 'Custom') {
      const h = this.customDurationHours();
      if (!h || h <= 0) { this.auctionError.set('Enter a valid custom duration in hours.'); return; }
      duration = `${h} Hours`;
    }
    if (!crop || !quantity || !price || !increment || !this.auctionStart()) { this.auctionError.set('Complete all auction fields with values greater than zero.'); return; }
    const request: CreateFarmerAuctionRequest = { cropId: crop.id, quantity, unit: this.auctionUnit(), startingBidPrice: price, minimumBidIncrement: increment, startTimeUtc: new Date(this.auctionStart()).toISOString(), duration, description: null };
    this.savingAuction.set(true); 
    this.auctionService.createAuction(request).subscribe({ 
      next: auction => { 
        this.loadAuctions();
        this.loadStockSummary(crop.id);
        this.savingAuction.set(false); 
        this.closeAuctionModal(); 
      }, 
      error: err => { 
        this.auctionError.set(err?.status === 404 ? 'Auction API is unavailable. Restart the backend and try again.' : err?.error?.message || 'Unable to create auction.'); 
        this.savingAuction.set(false); 
      } 
    });
  }
  cancelAuction(id: string): void { this.auctionService.cancelAuction(id).subscribe({ next: () => this.loadAuctions(), error: err => this.auctionError.set(err?.error?.message || 'Unable to cancel auction.') }); }

  loadStockSummary(cropId: string): void {
    this.cropService.getCropStock(cropId).subscribe({
      next: (summary) => {
        this.stockSummary.set(summary);
      },
      error: () => {
        // Stock summary might return 400 for Planned/Growing crops, which is expected
      }
    });
  }

  selectMainImage(imageUrl: string): void {
    this.selectedImage.set(imageUrl);
    this.imageError.set(false);
  }

  onImageError(): void {
    this.imageError.set(true);
  }

  openStockModal(): void {
    this.stockErrorMessage.set(null);
    this.stockQuantity.set(null);
    this.stockNotes.set('');
    this.showStockModal.set(true);
  }

  closeStockModal(): void {
    this.showStockModal.set(false);
  }

  saveStock(): void {
    const c = this.crop();
    const qty = this.stockQuantity();

    if (!c) return;

    if (qty === null || qty <= 0) {
      this.stockErrorMessage.set('Stock quantity must be greater than zero.');
      return;
    }

    this.savingStock.set(true);
    this.stockErrorMessage.set(null);

    this.cropService.addCropStock(c.id, {
      quantity: qty,
      unit: this.stockUnit(),
      transactionType: this.stockTransactionType(),
      notes: this.stockNotes().trim() || null
    }).subscribe({
      next: (updatedSummary) => {
        this.stockSummary.set(updatedSummary);
        this.savingStock.set(false);
        this.closeStockModal();
      },
      error: (err) => {
        this.stockErrorMessage.set(err?.error?.message || 'Failed to add stock record.');
        this.savingStock.set(false);
      }
    });
  }

  openHistoryModal(): void {
    const c = this.crop();
    if (!c) return;

    this.showHistoryModal.set(true);
    this.loadingHistory.set(true);

    this.cropService.getCropStockHistory(c.id).subscribe({
      next: (history) => {
        this.stockHistory.set(history);
        this.loadingHistory.set(false);
      },
      error: () => {
        this.loadingHistory.set(false);
      }
    });
  }

  closeHistoryModal(): void {
    this.showHistoryModal.set(false);
  }

  openDeleteModal(): void {
    this.showDeleteModal.set(true);
  }

  closeDeleteModal(): void {
    this.showDeleteModal.set(false);
  }

  confirmDelete(): void {
    const c = this.crop();
    if (!c) return;

    this.deleting.set(true);
    this.cropService.deleteCrop(c.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.router.navigate(['/farmer/crops']);
      },
      error: (err) => {
        this.deleting.set(false);
        alert(err?.error?.message || 'Failed to delete crop.');
      }
    });
  }

  isStockEligible(): boolean {
    const c = this.crop();
    if (!c) return false;
    return c.status === 'ReadyForHarvest' || c.status === 'Harvested' || c.status === 'Sold' || c.status === 'Archived';
  }
}
