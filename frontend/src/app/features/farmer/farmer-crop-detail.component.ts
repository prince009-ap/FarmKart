import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FarmerCropService } from './farmer-crop.service';
import { CropImage, FarmerCrop } from '../../core/models/farmer-crop.models';

@Component({
  selector: 'app-farmer-crop-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './farmer-crop-detail.component.html'
})
export class FarmerCropDetailComponent implements OnInit {
  private readonly cropService = inject(FarmerCropService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  crop = signal<FarmerCrop | null>(null);
  selectedImage = signal<string | null>(null);
  imageError = signal<boolean>(false);

  showDeleteModal = signal<boolean>(false);
  deleting = signal<boolean>(false);

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
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Unable to load crop details.');
        this.loading.set(false);
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
}
