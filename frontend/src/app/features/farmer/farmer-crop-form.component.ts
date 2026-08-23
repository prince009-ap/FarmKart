import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { FarmerCropService } from './farmer-crop.service';
import { CreateCropRequest, CropImage, UpdateCropRequest } from '../../core/models/farmer-crop.models';
import { AiConversationService } from '../../core/services/ai-conversation.service';
import { StartAiConversationRequest } from '../../core/models/ai-conversation.models';

interface PendingImageUpload {
  file: File;
  previewUrl: string;
  isPrimary: boolean;
}

@Component({
  selector: 'app-farmer-crop-form',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatDatepickerModule,
    MatNativeDateModule
  ],
  templateUrl: './farmer-crop-form.component.html'
})
export class FarmerCropFormComponent implements OnInit {
  private readonly cropService = inject(FarmerCropService);
  private readonly conversationService = inject(AiConversationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  isEditMode = signal<boolean>(false);
  cropId = signal<string | null>(null);
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  // Form Fields
  cropName = signal<string>('');
  cropType = signal<string>('Cereal');
  variety = signal<string>('');
  area = signal<number | null>(null);
  areaUnit = signal<string>('Bigha');
  sowingDate = signal<Date | null>(null);
  expectedHarvestDate = signal<Date | null>(null);
  actualHarvestDate = signal<Date | null>(null);
  status = signal<string>('Growing');
  description = signal<string>('');

  // Image Management State
  existingImages = signal<CropImage[]>([]);
  pendingImages = signal<PendingImageUpload[]>([]);
  uploadingImage = signal<boolean>(false);
  failedExistingImages = signal<Record<string, boolean>>({});

  readonly cropCategories = [
    'Cereal',
    'Pulses',
    'Vegetable',
    'Fruit',
    'Oilseed',
    'Spices',
    'Cash Crop',
    'Fodder',
    'Other'
  ];

  readonly areaUnits = [
    { label: 'Bigha', value: 'Bigha' },
    { label: 'Acre', value: 'Acre' },
    { label: 'Hectare', value: 'Hectare' }
  ];

  readonly statuses = [
    { label: 'Planned', value: 'Planned' },
    { label: 'Growing', value: 'Growing' },
    { label: 'Ready For Harvest', value: 'ReadyForHarvest' },
    { label: 'Harvested', value: 'Harvested' },
    { label: 'Sold', value: 'Sold' },
    { label: 'Archived', value: 'Archived' }
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode.set(true);
      this.cropId.set(id);
      this.loadCropDetails(id);
    }

    const taskName = this.isEditMode() ? 'update_farmer_crop' : 'create_farmer_crop';

    this.conversationService.fieldUpdated$.subscribe((evt) => {
      if ((evt.taskName === 'create_farmer_crop' || evt.taskName === 'update_farmer_crop') && evt.field && evt.value != null) {
        if (evt.field === 'cropName') this.cropName.set(evt.value);
        if (evt.field === 'cropType') this.cropType.set(evt.value);
        if (evt.field === 'variety') this.variety.set(evt.value);
        if (evt.field === 'area') this.area.set(parseFloat(evt.value) || null);
        if (evt.field === 'areaUnit') this.areaUnit.set(evt.value);
        if (evt.field === 'status') this.status.set(evt.value);
        if (evt.field === 'description') this.description.set(evt.value);
      }
    });

    this.conversationService.formCompleted$.subscribe((evt) => {
      if (evt.taskName === 'create_farmer_crop' || evt.taskName === 'update_farmer_crop') {
        this.saveCrop();
      }
    });
  }

  startCropAi(): void {
    const taskName = this.isEditMode() ? 'update_farmer_crop' : 'create_farmer_crop';
    const initialData: Record<string, string | null> = {
      cropName: this.cropName() || null,
      cropType: this.cropType() || 'Cereal',
      variety: this.variety() || null,
      area: this.area() !== null ? String(this.area()) : null,
      areaUnit: this.areaUnit() || 'Bigha',
      status: this.status() || 'Growing',
      description: this.description() || null
    };

    const request: StartAiConversationRequest = {
      taskName,
      pageName: 'farmer_crop_form',
      fields: [
        { name: 'cropName', label: 'Crop Name', type: 'text', required: true, description: 'Name of the crop e.g. Wheat' },
        { name: 'cropType', label: 'Crop Category', type: 'select', required: true, description: 'Category e.g. Cereal, Pulses, Vegetable, Fruit', options: this.cropCategories },
        { name: 'variety', label: 'Variety', type: 'text', required: false, description: 'Crop variety e.g. Sharbati' },
        { name: 'area', label: 'Farm Area', type: 'decimal', required: true, description: 'Planted area number' },
        { name: 'areaUnit', label: 'Area Unit', type: 'select', required: true, description: 'Unit of area', options: ['Bigha', 'Acre', 'Hectare'] },
        { name: 'status', label: 'Crop Status', type: 'select', required: true, description: 'Current status', options: ['Planned', 'Growing', 'ReadyForHarvest', 'Harvested', 'Sold', 'Archived'] },
        { name: 'description', label: 'Description', type: 'textarea', required: false, description: 'Additional crop details' }
      ],
      initialData
    };

    this.conversationService.startConversation(request).subscribe();
  }

  loadCropDetails(id: string): void {
    this.loading.set(true);
    this.cropService.getCropById(id).subscribe({
      next: (crop) => {
        this.cropName.set(crop.cropName);
        this.cropType.set(crop.cropType || 'Cereal');
        this.variety.set(crop.variety || '');
        this.area.set(crop.area);
        this.areaUnit.set(crop.areaUnit || 'Bigha');
        this.sowingDate.set(crop.sowingDate ? new Date(crop.sowingDate) : null);
        this.expectedHarvestDate.set(crop.expectedHarvestDate ? new Date(crop.expectedHarvestDate) : null);
        this.actualHarvestDate.set(crop.actualHarvestDate ? new Date(crop.actualHarvestDate) : null);
        this.status.set(crop.status || 'Growing');
        this.description.set(crop.description || '');
        this.existingImages.set(crop.images || []);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || 'Unable to load crop details for editing.');
        this.loading.set(false);
      }
    });
  }

  handleExistingImageError(imgId: string): void {
    this.failedExistingImages.update(map => ({ ...map, [imgId]: true }));
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    this.errorMessage.set(null);
    const files = Array.from(input.files);

    const totalCount = this.existingImages().length + this.pendingImages().length;
    if (totalCount + files.length > 5) {
      this.errorMessage.set('Maximum 5 images per crop allowed.');
      return;
    }

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];

    for (const file of files) {
      if (!allowedTypes.includes(file.type.toLowerCase())) {
        this.errorMessage.set(`Invalid file format '${file.name}'. Only JPG, PNG, and WEBP are supported.`);
        return;
      }

      if (file.size > 20 * 1024 * 1024) {
        this.errorMessage.set(`File '${file.name}' exceeds the maximum allowed size of 20 MB.`);
        return;
      }

      const reader = new FileReader();
      reader.onload = (e) => {
        const previewUrl = e.target?.result as string;

        if (this.isEditMode() && this.cropId()) {
          this.uploadSingleFileInEditMode(file);
        } else {
          const isFirst = this.pendingImages().length === 0;
          this.pendingImages.update(list => [...list, { file, previewUrl, isPrimary: isFirst }]);
        }
      };
      reader.readAsDataURL(file);
    }

    input.value = '';
  }

  uploadSingleFileInEditMode(file: File): void {
    const cropId = this.cropId();
    if (!cropId) return;

    this.uploadingImage.set(true);
    const isPrimary = this.existingImages().length === 0;

    this.cropService.uploadCropImage(cropId, file, isPrimary).subscribe({
      next: (newImg) => {
        this.existingImages.update(list => [...list, newImg]);
        this.uploadingImage.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || 'Failed to upload image.');
        this.uploadingImage.set(false);
      }
    });
  }

  removeExistingImage(img: CropImage): void {
    const cropId = this.cropId();
    if (!cropId) return;

    this.cropService.deleteCropImage(cropId, img.id).subscribe({
      next: () => {
        this.existingImages.update(list => list.filter(i => i.id !== img.id));
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || 'Failed to delete image.');
      }
    });
  }

  setExistingPrimary(img: CropImage): void {
    const cropId = this.cropId();
    if (!cropId) return;

    this.cropService.setPrimaryCropImage(cropId, img.id).subscribe({
      next: (updatedCrop) => {
        this.existingImages.set(updatedCrop.images || []);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || 'Failed to set primary image.');
      }
    });
  }

  removePendingImage(index: number): void {
    this.pendingImages.update(list => {
      const newList = [...list];
      newList.splice(index, 1);
      if (newList.length > 0 && !newList.some(p => p.isPrimary)) {
        newList[0].isPrimary = true;
      }
      return newList;
    });
  }

  setPendingPrimary(index: number): void {
    this.pendingImages.update(list =>
      list.map((item, idx) => ({ ...item, isPrimary: idx === index }))
    );
  }

  private formatDateForApi(d: Date | null): string | null {
    if (!d) return null;
    const dateObj = new Date(d);
    if (isNaN(dateObj.getTime())) return null;
    const year = dateObj.getFullYear();
    const month = String(dateObj.getMonth() + 1).padStart(2, '0');
    const day = String(dateObj.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  saveCrop(): void {
    this.errorMessage.set(null);

    if (!this.cropName().trim()) {
      this.errorMessage.set('Crop name is required.');
      return;
    }

    if (!this.cropType().trim()) {
      this.errorMessage.set('Crop type is required.');
      return;
    }

    const areaVal = this.area();
    if (areaVal === null || areaVal <= 0) {
      this.errorMessage.set('Cultivated area must be greater than zero.');
      return;
    }

    const sowDate = this.sowingDate();
    const expHarvest = this.expectedHarvestDate();
    const actHarvest = this.actualHarvestDate();

    if (sowDate && expHarvest && expHarvest < sowDate) {
      this.errorMessage.set('Expected harvest date cannot be before planting date.');
      return;
    }

    if (sowDate && actHarvest && actHarvest < sowDate) {
      this.errorMessage.set('Actual harvest date cannot be before planting date.');
      return;
    }

    this.saving.set(true);

    const payload: CreateCropRequest | UpdateCropRequest = {
      cropName: this.cropName().trim(),
      cropType: this.cropType().trim(),
      variety: this.variety().trim() || null,
      area: areaVal,
      areaUnit: this.areaUnit(),
      sowingDate: this.formatDateForApi(sowDate),
      expectedHarvestDate: this.formatDateForApi(expHarvest),
      actualHarvestDate: this.formatDateForApi(actHarvest),
      status: this.status(),
      description: this.description().trim() || null
    };

    if (this.isEditMode() && this.cropId()) {
      this.cropService.updateCrop(this.cropId()!, payload).subscribe({
        next: () => {
          this.saving.set(false);
          this.router.navigate(['/farmer/crops']);
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.message || 'Failed to update crop.');
          this.saving.set(false);
        }
      });
    } else {
      this.cropService.createCrop(payload).subscribe({
        next: async (newCrop) => {
          const pending = this.pendingImages();
          if (pending.length > 0) {
            for (const item of pending) {
              try {
                await this.cropService.uploadCropImage(newCrop.id, item.file, item.isPrimary).toPromise();
              } catch {
                // Ignore single file upload failure during batch
              }
            }
          }
          this.saving.set(false);
          this.router.navigate(['/farmer/crops']);
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.message || 'Failed to add crop.');
          this.saving.set(false);
        }
      });
    }
  }
}
