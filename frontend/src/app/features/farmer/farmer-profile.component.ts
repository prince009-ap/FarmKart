import { Component, OnInit, inject, signal } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule,
  AbstractControl,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';
import { FarmerProfileService } from './farmer-profile.service';
import { FarmerProfile, FarmerProfileUpdateRequest, FarmSizeUnit } from '../../core/models/farmer.models';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';
import { AiConversationService } from '../../core/services/ai-conversation.service';
import { StartAiConversationRequest } from '../../core/models/ai-conversation.models';
import { LanguageService } from '../../core/services/language.service';

@Component({
  selector: 'app-farmer-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatSnackBarModule,
    MatDividerModule,
  ],
  templateUrl: './farmer-profile.component.html',
})
export class FarmerProfileComponent implements OnInit {
  private readonly profileService = inject(FarmerProfileService);
  private readonly conversationService = inject(AiConversationService);
  private readonly languageService = inject(LanguageService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  readonly farmSizeUnitOptions: FarmSizeUnit[] = ['Vigha', 'Acre', 'Hectare'];

  profile = signal<FarmerProfile | null>(null);
  loading = signal(true);
  saving = signal(false);
  editMode = signal(false);
  loadError = signal<string | null>(null);

  selectedFile = signal<File | null>(null);
  selectedFileName = signal<string>('');
  selectedImagePreview = signal<string | null>(null);
  uploadingImage = signal(false);
  imageTimestamp = signal(Date.now());
  avatarLoadFailed = signal(false);

  profileForm!: FormGroup;

  ngOnInit(): void {
    this.buildForm();
    this.loadProfile();

    this.conversationService.fieldUpdated$.subscribe((evt) => {
      if (evt.taskName === 'update_farmer_profile' && evt.field && evt.value != null) {
        if (evt.field === 'farmSize') {
          this.profileForm.patchValue({ farmSize: parseFloat(evt.value) || null });
        } else {
          this.profileForm.patchValue({ [evt.field]: evt.value });
        }
      }
    });

    this.conversationService.formCompleted$.subscribe((evt) => {
      if (evt.taskName === 'update_farmer_profile') {
        this.saveProfile();
      }
    });
  }

  private buildForm(): void {
    this.profileForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(150)]],
      phone: ['', [Validators.required, Validators.maxLength(20)]],
      address: ['', [Validators.required]],
      farmName: ['', [Validators.maxLength(150)]],
      farmSize: [null, [Validators.min(0)]],
      farmSizeUnit: ['Vigha' as FarmSizeUnit, [Validators.required]],
      farmLocation: ['', [Validators.maxLength(250)]],
    });
  }

  private loadProfile(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.profileService.getProfile().subscribe({
      next: (data) => {
        this.profile.set(data);
        this.patchForm(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 404) {
          this.loadError.set('Profile not found. Please contact support.');
        } else if (err.status === 401) {
          this.loadError.set('You are not authenticated. Please log in again.');
        } else if (err.status === 403) {
          this.loadError.set('You do not have permission to view this profile.');
        } else if (err.status === 0) {
          this.loadError.set('Cannot reach the server. Please check your connection.');
        } else {
          this.loadError.set('Failed to load profile. Please try again.');
        }
      },
    });
  }

  private patchForm(data: FarmerProfile): void {
    this.profileForm.patchValue({
      fullName: data.fullName,
      phone: data.phone,
      address: data.address,
      farmName: data.farmName ?? '',
      farmSize: data.farmSize,
      farmSizeUnit: data.farmSizeUnit ?? 'Vigha',
      farmLocation: data.farmLocation ?? '',
    });
  }

  enterEditMode(): void {
    this.editMode.set(true);
  }

  startProfileAi(): void {
    this.enterEditMode();
    const current = this.profileForm.value;
    const initialData: Record<string, string | null> = {
      fullName: current.fullName || null,
      phone: current.phone || null,
      address: current.address || null,
      farmName: current.farmName || null,
      farmSize: current.farmSize ? String(current.farmSize) : null,
      farmSizeUnit: current.farmSizeUnit || 'Vigha',
      farmLocation: current.farmLocation || null
    };

    const request: StartAiConversationRequest = {
      taskName: 'update_farmer_profile',
      pageName: 'farmer_profile',
      language: this.languageService.currentLanguage(),
      fields: [
        { name: 'fullName', label: 'Full Name', type: 'text', required: true, description: 'Farmer full name' },
        { name: 'phone', label: 'Phone Number', type: 'phone', required: true, description: 'Contact phone number' },
        { name: 'address', label: 'Address', type: 'text', required: true, description: 'Farmer address' },
        { name: 'farmName', label: 'Farm Name', type: 'text', required: false, description: 'Name of the farm' },
        { name: 'farmSize', label: 'Farm Size', type: 'decimal', required: false, description: 'Size of the farm in numbers' },
        { name: 'farmSizeUnit', label: 'Farm Size Unit', type: 'select', required: false, description: 'Unit of farm size', options: ['Vigha', 'Acre', 'Hectare'] },
        { name: 'farmLocation', label: 'Farm Location', type: 'text', required: false, description: 'Location of the farm' }
      ],
      initialData
    };

    this.conversationService.startConversation(request).subscribe();
  }

  cancelEdit(): void {
    const current = this.profile();
    if (current) {
      this.patchForm(current);
    }
    this.editMode.set(false);
  }

  saveProfile(): void {
    if (this.profileForm.invalid || this.saving()) {
      return;
    }

    const raw = this.profileForm.getRawValue();
    const request: FarmerProfileUpdateRequest = {
      fullName: raw.fullName,
      phone: raw.phone,
      address: raw.address,
      farmName: raw.farmName?.trim() || null,
      farmSize: raw.farmSize !== '' && raw.farmSize !== null ? Number(raw.farmSize) : null,
      farmSizeUnit: raw.farmSizeUnit || null,
      farmLocation: raw.farmLocation?.trim() || null,
    };

    this.saving.set(true);

    this.profileService.updateProfile(request).subscribe({
      next: (updated) => {
        this.profile.set(updated);
        this.saving.set(false);
        this.editMode.set(false);
        this.snackBar.open('Profile updated successfully.', 'Close', { duration: 3000 });
      },
      error: (err) => {
        this.saving.set(false);
        if (err.status === 400) {
          const msg = err.error?.message ?? 'Invalid data. Please review your inputs.';
          this.snackBar.open(msg, 'Close', { duration: 5000 });
        } else if (err.status === 401) {
          this.snackBar.open('Session expired. Please log in again.', 'Close', { duration: 5000 });
        } else if (err.status === 404) {
          this.snackBar.open('Profile not found.', 'Close', { duration: 5000 });
        } else if (err.status === 0) {
          this.snackBar.open('Cannot reach the server. Please check your connection.', 'Close', { duration: 5000 });
        } else {
          this.snackBar.open('Failed to save profile. Please try again.', 'Close', { duration: 5000 });
        }
      },
    });
  }

  field(name: string): AbstractControl | null {
    return this.profileForm.get(name);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];
    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
    const allowedExts = ['.jpg', '.jpeg', '.png', '.webp'];
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

    if (!allowedTypes.includes(file.type) && !allowedExts.includes(ext)) {
      this.snackBar.open('Image must be JPG, PNG or WEBP.', 'Close', { duration: 4000 });
      input.value = '';
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.snackBar.open('Image size must be less than 5 MB.', 'Close', { duration: 4000 });
      input.value = '';
      return;
    }

    this.selectedFile.set(file);
    this.selectedFileName.set(file.name);

    const reader = new FileReader();
    reader.onload = () => {
      this.selectedImagePreview.set(reader.result as string);
    };
    reader.readAsDataURL(file);
  }

  cancelFileSelection(): void {
    this.selectedFile.set(null);
    this.selectedFileName.set('');
    this.selectedImagePreview.set(null);
  }

  uploadSelectedImage(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.uploadingImage.set(true);
    this.profileService.uploadProfileImage(file).subscribe({
      next: (res) => {
        this.profile.set(res);
        this.imageTimestamp.set(Date.now());
        this.avatarLoadFailed.set(false);
        this.cancelFileSelection();
        this.uploadingImage.set(false);
        this.authService.updateUserProfileImage(res.profileImageUrl || null);
        this.snackBar.open('Profile image updated successfully.', 'Close', { duration: 3000 });
      },
      error: (err) => {
        this.uploadingImage.set(false);
        const msg = err?.error?.message || 'Unable to upload profile image.';
        this.snackBar.open(msg, 'Close', { duration: 5000 });
      }
    });
  }

  confirmRemoveImage(): void {
    if (!confirm('Remove your profile image?')) return;

    this.uploadingImage.set(true);
    this.profileService.removeProfileImage().subscribe({
      next: (res) => {
        this.profile.set(res);
        this.imageTimestamp.set(Date.now());
        this.avatarLoadFailed.set(false);
        this.cancelFileSelection();
        this.uploadingImage.set(false);
        this.authService.updateUserProfileImage(null);
        this.snackBar.open('Profile image removed successfully.', 'Close', { duration: 3000 });
      },
      error: (err) => {
        this.uploadingImage.set(false);
        const msg = err?.error?.message || 'Unable to remove profile image.';
        this.snackBar.open(msg, 'Close', { duration: 5000 });
      }
    });
  }

  getDisplayAvatarUrl(): string | null {
    if (this.avatarLoadFailed() || !this.profile()?.profileImageUrl) return null;
    let url = this.profile()!.profileImageUrl!;
    if (url.startsWith('/')) {
      url = `${environment.apiUrl.replace(/\/api$/, '')}${url}`;
    }
    return `${url}?v=${this.imageTimestamp()}`;
  }

  onAvatarError(): void {
    this.avatarLoadFailed.set(true);
  }

  getInitials(name?: string | null): string {
    if (!name) return 'F';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
  }
}
