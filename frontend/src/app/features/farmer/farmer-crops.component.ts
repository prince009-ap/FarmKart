import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { FarmerCropService } from './farmer-crop.service';
import { FarmerCrop } from '../../core/models/farmer-crop.models';

@Component({
  selector: 'app-farmer-crops',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './farmer-crops.component.html'
})
export class FarmerCropsComponent implements OnInit {
  private readonly cropService = inject(FarmerCropService);

  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  crops = signal<FarmerCrop[]>([]);
  searchTerm = signal<string>('');
  selectedStatus = signal<string>('');

  cropToDelete = signal<FarmerCrop | null>(null);
  deleting = signal<boolean>(false);
  failedImages = signal<Record<string, boolean>>({});

  ngOnInit(): void {
    this.loadCrops();
  }

  loadCrops(): void {
    this.loading.set(true);
    this.error.set(null);

    this.cropService.getCrops().subscribe({
      next: (data) => {
        this.crops.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Unable to load crops. Please try again.');
        this.loading.set(false);
      }
    });
  }

  handleImageError(cropId: string): void {
    this.failedImages.update(map => ({ ...map, [cropId]: true }));
  }

  hasValidImage(crop: FarmerCrop): boolean {
    return !!crop.primaryImageUrl && !this.failedImages()[crop.id];
  }

  get filteredCrops(): FarmerCrop[] {
    const term = this.searchTerm().trim().toLowerCase();
    const status = this.selectedStatus();

    return this.crops().filter(crop => {
      const matchesTerm = !term ||
        crop.cropName.toLowerCase().includes(term) ||
        crop.cropType.toLowerCase().includes(term) ||
        (crop.variety && crop.variety.toLowerCase().includes(term));

      const matchesStatus = !status || crop.status === status;

      return matchesTerm && matchesStatus;
    });
  }

  openDeleteModal(crop: FarmerCrop): void {
    this.cropToDelete.set(crop);
  }

  closeDeleteModal(): void {
    this.cropToDelete.set(null);
  }

  confirmDelete(): void {
    const crop = this.cropToDelete();
    if (!crop) return;

    this.deleting.set(true);
    this.cropService.deleteCrop(crop.id).subscribe({
      next: () => {
        this.crops.update(list => list.filter(c => c.id !== crop.id));
        this.deleting.set(false);
        this.closeDeleteModal();
      },
      error: (err) => {
        this.deleting.set(false);
        alert(err?.error?.message || 'Failed to delete crop.');
      }
    });
  }
}
