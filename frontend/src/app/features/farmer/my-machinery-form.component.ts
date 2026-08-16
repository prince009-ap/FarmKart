import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MachineryService } from '../../core/services/machinery.service';
import { MACHINERY_CATEGORIES, MachineryResponse } from '../../core/models/machinery.models';

@Component({
  selector: 'app-my-machinery-form',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './my-machinery-form.component.html'
})
export class MyMachineryFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly machineryService = inject(MachineryService);
  private readonly snackBar = inject(MatSnackBar);

  isEditMode = signal<boolean>(false);
  machineryId = signal<string | null>(null);
  isLoading = signal<boolean>(false);
  isSaving = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  categories = MACHINERY_CATEGORIES;

  // Form Model
  name = signal<string>('');
  category = signal<string>('Tractor');
  brand = signal<string>('');
  model = signal<string>('');
  manufacturingYear = signal<number | undefined>(undefined);
  description = signal<string>('');
  dailyRent = signal<number | undefined>(undefined);
  securityDeposit = signal<number | undefined>(undefined);
  isDriverIncluded = signal<boolean>(false);
  isFuelIncluded = signal<boolean>(false);
  location = signal<string>('');
  city = signal<string>('');
  state = signal<string>('');
  pincode = signal<string>('');

  // Image Upload State
  existingImages = signal<any[]>([]);
  selectedFile = signal<File | null>(null);
  isUploadingImage = signal<boolean>(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode.set(true);
      this.machineryId.set(id);
      this.loadMachinery(id);
    }
  }

  loadMachinery(id: string): void {
    this.isLoading.set(true);
    this.machineryService.getMachineryById(id).subscribe({
      next: (m) => {
        this.name.set(m.name);
        this.category.set(m.category);
        this.brand.set(m.brand || '');
        this.model.set(m.model || '');
        this.manufacturingYear.set(m.manufacturingYear);
        this.description.set(m.description || '');
        this.dailyRent.set(m.dailyRent);
        this.securityDeposit.set(m.securityDeposit);
        this.isDriverIncluded.set(m.isDriverIncluded);
        this.isFuelIncluded.set(m.isFuelIncluded);
        this.location.set(m.location);
        this.city.set(m.city || '');
        this.state.set(m.state || '');
        this.pincode.set(m.pincode || '');
        this.existingImages.set(m.images || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load machinery details.');
        this.isLoading.set(false);
      }
    });
  }

  saveMachinery(): void {
    if (!this.name() || !this.category() || !this.dailyRent() || !this.location()) {
      this.snackBar.open('Please fill out all required fields.', 'Close', { duration: 3000 });
      return;
    }

    this.isSaving.set(true);

    if (this.isEditMode()) {
      this.machineryService.updateMachinery(this.machineryId()!, {
        name: this.name(),
        category: this.category(),
        brand: this.brand() || undefined,
        model: this.model() || undefined,
        manufacturingYear: this.manufacturingYear(),
        description: this.description() || undefined,
        dailyRent: this.dailyRent(),
        securityDeposit: this.securityDeposit(),
        isDriverIncluded: this.isDriverIncluded(),
        isFuelIncluded: this.isFuelIncluded(),
        location: this.location(),
        city: this.city() || undefined,
        state: this.state() || undefined,
        pincode: this.pincode() || undefined
      }).subscribe({
        next: (m) => {
          this.isSaving.set(false);
          this.snackBar.open('Machinery updated successfully!', 'Close', { duration: 3000 });
          this.router.navigate(['/farmer/machinery']);
        },
        error: (err) => {
          this.isSaving.set(false);
          this.snackBar.open(err?.error?.message || 'Failed to update machinery.', 'Close', { duration: 4000 });
        }
      });
    } else {
      this.machineryService.createMachinery({
        name: this.name(),
        category: this.category(),
        brand: this.brand() || undefined,
        model: this.model() || undefined,
        manufacturingYear: this.manufacturingYear(),
        description: this.description() || undefined,
        dailyRent: this.dailyRent()!,
        securityDeposit: this.securityDeposit() || 0,
        isDriverIncluded: this.isDriverIncluded(),
        isFuelIncluded: this.isFuelIncluded(),
        location: this.location(),
        city: this.city() || undefined,
        state: this.state() || undefined,
        pincode: this.pincode() || undefined
      }).subscribe({
        next: (m) => {
          this.isSaving.set(false);
          this.snackBar.open('Machinery created! You can now upload images.', 'Close', { duration: 3000 });
          this.router.navigate(['/farmer/machinery', m.id, 'edit']);
        },
        error: (err) => {
          this.isSaving.set(false);
          this.snackBar.open(err?.error?.message || 'Failed to create machinery.', 'Close', { duration: 4000 });
        }
      });
    }
  }

  onFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      this.selectedFile.set(file);
      this.uploadImage();
    }
  }

  uploadImage(): void {
    const file = this.selectedFile();
    const id = this.machineryId();
    if (!file || !id) return;

    this.isUploadingImage.set(true);
    this.machineryService.uploadImage(id, file).subscribe({
      next: (img) => {
        this.isUploadingImage.set(false);
        this.selectedFile.set(null);
        this.snackBar.open('Image uploaded successfully!', 'Close', { duration: 3000 });
        this.loadMachinery(id);
      },
      error: (err) => {
        this.isUploadingImage.set(false);
        this.snackBar.open(err?.error?.message || 'Image upload failed.', 'Close', { duration: 4000 });
      }
    });
  }

  deleteImage(imageId: string): void {
    const id = this.machineryId();
    if (!id) return;

    this.machineryService.deleteImage(id, imageId).subscribe({
      next: () => {
        this.snackBar.open('Image deleted.', 'Close', { duration: 3000 });
        this.loadMachinery(id);
      },
      error: () => {
        this.snackBar.open('Failed to delete image.', 'Close', { duration: 3000 });
      }
    });
  }
}
