import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { CustomerProfileService } from '../../core/services/customer-profile.service';
import { AuthService } from '../../core/services/auth.service';
import { CustomerProfileResponse } from '../../core/models/customer-profile.models';
import { environment } from '../../../environments/environment';
import { AiConversationService } from '../../core/services/ai-conversation.service';
import { StartAiConversationRequest } from '../../core/models/ai-conversation.models';
import { LanguageService } from '../../core/services/language.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

@Component({
  selector: 'app-customer-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslatePipe],
  template: `
    <div class="min-h-screen bg-slate-50 py-8 px-4 sm:px-6 lg:px-8">
      <div class="max-w-4xl mx-auto space-y-8">
        
        <!-- Header -->
        <div class="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-slate-200/80 flex items-center justify-between gap-4">
          <div class="flex items-center gap-4">
            <div class="w-12 h-12 rounded-2xl bg-emerald-100 text-emerald-700 flex items-center justify-center text-2xl font-bold">
              👤
            </div>
            <div>
              <h1 class="text-2xl font-bold text-slate-900">{{ 'profile.title' | translate }}</h1>
              <p class="text-xs text-slate-500 mt-1">{{ 'profile.subtitle' | translate }}</p>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <button *ngIf="!isEditing" (click)="startProfileAi()" class="px-4 py-2 text-xs font-semibold text-emerald-800 bg-emerald-100/80 hover:bg-emerald-200/80 rounded-xl transition flex items-center gap-2">
              <span>{{ 'farmer.fillProfileWithAi' | translate }}</span>
            </button>
            <button *ngIf="!isEditing" (click)="toggleEdit()" class="px-4 py-2 text-xs font-semibold text-emerald-700 bg-emerald-50 hover:bg-emerald-100 rounded-xl transition flex items-center gap-2">
              <span>{{ 'common.edit' | translate }} {{ 'profile.title' | translate }}</span>
            </button>
          </div>
        </div>

        <!-- Global Alert Messages -->
        <div *ngIf="successMessage" class="bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs rounded-2xl p-4 flex items-center gap-3">
          <span class="text-lg">✅</span>
          <p class="font-medium">{{ successMessage }}</p>
        </div>

        <div *ngIf="errorMessage" class="bg-red-50 border border-red-200 text-red-800 text-xs rounded-2xl p-4 flex items-center justify-between gap-3">
          <div class="flex items-center gap-3">
            <span class="text-lg">⚠️</span>
            <p class="font-medium">{{ errorMessage }}</p>
          </div>
          <button (click)="loadProfile()" class="px-3 py-1 text-[11px] font-bold text-red-700 bg-red-100 hover:bg-red-200 rounded-lg transition">
            {{ 'common.refresh' | translate }}
          </button>
        </div>

        <!-- Profile Card -->
        <div class="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-slate-200/80 space-y-8">
          
          <!-- Profile Overview Header -->
          <div class="flex flex-col sm:flex-row items-center sm:items-start gap-6 pb-6 border-b border-slate-100">
            <!-- Avatar Display -->
            <div class="relative group">
              <div class="w-24 h-24 sm:w-28 sm:h-28 rounded-full overflow-hidden bg-emerald-100 border-4 border-white shadow-md flex items-center justify-center text-emerald-800 text-3xl font-extrabold">
                <img *ngIf="getDisplayAvatarUrl()" [src]="getDisplayAvatarUrl()" [alt]="profile?.fullName" class="w-full h-full object-cover" (error)="onAvatarError()" />
                <span *ngIf="!getDisplayAvatarUrl()">{{ getInitials(profile?.fullName) }}</span>
              </div>
            </div>

            <div class="text-center sm:text-left space-y-2 flex-1">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div>
                  <h2 class="text-xl font-bold text-slate-900">{{ profile?.fullName || ('auth.customer' | translate) }}</h2>
                  <div class="flex items-center justify-center sm:justify-start gap-2 mt-1">
                    <span class="inline-flex items-center px-3 py-0.5 text-xs font-bold text-emerald-800 bg-emerald-100 rounded-full">
                      {{ 'auth.customer' | translate }}
                    </span>
                    <span *ngIf="profile?.createdAtUtc" class="text-xs text-slate-400">
                      {{ profile?.createdAtUtc | date:'mediumDate' }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- READ VIEW -->
          <div *ngIf="!isEditing" class="grid grid-cols-1 sm:grid-cols-2 gap-6">
            <div class="p-4 rounded-2xl bg-slate-50/70 border border-slate-100">
              <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">{{ 'auth.fullName' | translate }}</label>
              <p class="text-sm font-semibold text-slate-800">{{ profile?.fullName || '—' }}</p>
            </div>

            <div class="p-4 rounded-2xl bg-slate-50/70 border border-slate-100">
              <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">{{ 'auth.email' | translate }}</label>
              <p class="text-sm font-semibold text-slate-800">{{ profile?.email || '—' }}</p>
            </div>

            <div class="p-4 rounded-2xl bg-slate-50/70 border border-slate-100">
              <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">{{ 'auth.phone' | translate }}</label>
              <p class="text-sm font-semibold text-slate-800">{{ profile?.phone || '—' }}</p>
            </div>

            <div class="p-4 rounded-2xl bg-slate-50/70 border border-slate-100">
              <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">{{ 'profile.homeAddress' | translate }}</label>
              <p class="text-sm font-semibold text-slate-800">{{ profile?.address || '—' }}</p>
            </div>
          </div>

          <!-- EDIT VIEW -->
          <div *ngIf="isEditing" class="space-y-6">
            
            <!-- PROFILE IMAGE UPLOAD SECTION -->
            <div class="p-6 rounded-2xl bg-emerald-50/50 border border-emerald-100 space-y-4">
              <h3 class="text-xs font-bold uppercase tracking-wider text-emerald-900">{{ 'profile.profileImage' | translate }}</h3>
              
              <div class="flex flex-col sm:flex-row items-center gap-6">
                <!-- Preview Avatar -->
                <div class="w-20 h-20 rounded-full overflow-hidden bg-white border-2 border-emerald-300 shadow-sm flex items-center justify-center text-emerald-700 text-2xl font-bold">
                  <img *ngIf="selectedImagePreview || getDisplayAvatarUrl()" [src]="selectedImagePreview || getDisplayAvatarUrl()" alt="Preview" class="w-full h-full object-cover" />
                  <span *ngIf="!selectedImagePreview && !getDisplayAvatarUrl()">{{ getInitials(editFullName) }}</span>
                </div>

                <div class="space-y-3 text-center sm:text-left flex-1">
                  <div class="flex flex-wrap items-center justify-center sm:justify-start gap-3">
                    <label class="px-4 py-2 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl cursor-pointer transition shadow-sm inline-flex items-center gap-2">
                      <span>{{ 'profile.chooseImage' | translate }}</span>
                      <input type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="onFileSelected($event)" />
                    </label>

                    <button *ngIf="profile?.profileImageUrl" type="button" (click)="confirmRemoveImage()" [disabled]="isUploadingImage" class="px-4 py-2 text-xs font-semibold text-red-600 bg-red-50 hover:bg-red-100 rounded-xl transition">
                      {{ 'profile.removeImage' | translate }}
                    </button>
                  </div>

                  <p *ngIf="selectedFileName" class="text-xs text-emerald-800 font-medium">
                    Selected: <span class="font-bold">{{ selectedFileName }}</span>
                  </p>
                  <p class="text-[11px] text-slate-500">{{ 'profile.allowedFormats' | translate }}</p>
                </div>
              </div>

              <!-- Local Upload Button if new image selected -->
              <div *ngIf="selectedFile" class="pt-2">
                <button type="button" (click)="uploadSelectedImage()" [disabled]="isUploadingImage" class="px-4 py-2 text-xs font-bold text-white bg-slate-900 hover:bg-slate-800 disabled:opacity-50 rounded-xl transition shadow-sm flex items-center gap-2">
                  <span>{{ isUploadingImage ? ('common.loading' | translate) : ('profile.chooseImage' | translate) }}</span>
                </button>
              </div>
            </div>

            <!-- PROFILE FIELDS FORM -->
            <form (ngSubmit)="saveProfile()" class="space-y-5 max-w-xl">
              <div>
                <label for="cust-fullname" class="block text-xs font-semibold text-slate-700 mb-1">{{ 'auth.fullName' | translate }}</label>
                <input id="cust-fullname" name="cust-fullname" type="text" [(ngModel)]="editFullName" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50" required />
              </div>

              <div>
                <label for="cust-phone" class="block text-xs font-semibold text-slate-700 mb-1">{{ 'auth.phone' | translate }}</label>
                <input id="cust-phone" name="cust-phone" type="text" [(ngModel)]="editPhone" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50" />
              </div>

              <div>
                <label for="cust-address" class="block text-xs font-semibold text-slate-700 mb-1">{{ 'profile.homeAddress' | translate }}</label>
                <textarea id="cust-address" name="cust-address" rows="3" [(ngModel)]="editAddress" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50"></textarea>
              </div>

              <div class="flex items-center gap-3 pt-2">
                <button type="submit" [disabled]="isSaving" class="px-5 py-2.5 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 rounded-xl transition shadow-sm">
                  {{ isSaving ? ('common.loading' | translate) : ('common.saveChanges' | translate) }}
                </button>
                <button type="button" (click)="toggleEdit()" class="px-5 py-2.5 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition">
                  {{ 'common.cancel' | translate }}
                </button>
              </div>
            </form>

          </div>

        </div>

      </div>
    </div>
  `
})
export class CustomerProfileComponent implements OnInit {
  private readonly profileService = inject(CustomerProfileService);
  private readonly conversationService = inject(AiConversationService);
  private readonly languageService = inject(LanguageService);
  private readonly authService = inject(AuthService);

  profile: CustomerProfileResponse | null = null;
  isLoading = false;
  isSaving = false;
  isEditing = false;
  errorMessage = '';
  successMessage = '';

  editFullName = '';
  editPhone = '';
  editAddress = '';

  selectedFile: File | null = null;
  selectedFileName = '';
  selectedImagePreview: string | null = null;
  isUploadingImage = false;

  private imageTimestamp = Date.now();
  private avatarLoadFailed = false;

  ngOnInit(): void {
    this.loadProfile();

    this.conversationService.fieldUpdated$.subscribe((evt) => {
      if (evt.taskName === 'update_customer_profile' && evt.field && evt.value != null) {
        if (evt.field === 'fullName') this.editFullName = evt.value;
        if (evt.field === 'phone') this.editPhone = evt.value;
        if (evt.field === 'address') this.editAddress = evt.value;
      }
    });

    this.conversationService.formCompleted$.subscribe((evt) => {
      if (evt.taskName === 'update_customer_profile') {
        this.saveProfile();
      }
    });
  }

  startProfileAi(): void {
    if (!this.isEditing) {
      this.toggleEdit();
    }

    const request: StartAiConversationRequest = {
      taskName: 'update_customer_profile',
      pageName: 'customer_profile',
      language: this.languageService.currentLanguage(),
      fields: [
        { name: 'fullName', label: 'Full Name', type: 'text', required: true, description: 'Customer full name' },
        { name: 'phone', label: 'Phone Number', type: 'phone', required: true, description: 'Contact phone number' },
        { name: 'address', label: 'Delivery Address', type: 'text', required: false, description: 'Delivery address' }
      ],
      initialData: {
        fullName: this.editFullName || null,
        phone: this.editPhone || null,
        address: this.editAddress || null
      }
    };

    this.conversationService.startConversation(request).subscribe();
  }

  loadProfile(): void {
    this.errorMessage = '';
    this.profileService.getProfile().subscribe({
      next: (data) => {
        this.profile = data;
        this.editFullName = data.fullName;
        this.editPhone = data.phone;
        this.editAddress = data.address;
        this.avatarLoadFailed = false;
        
        if (data.profileImageUrl) {
          this.authService.updateUserProfileImage(data.profileImageUrl);
        }
      },
      error: (err) => {
        this.errorMessage = err?.error?.message || 'Unable to load profile information.';
      }
    });
  }

  toggleEdit(): void {
    this.isEditing = !this.isEditing;
    if (this.isEditing && this.profile) {
      this.editFullName = this.profile.fullName;
      this.editPhone = this.profile.phone;
      this.editAddress = this.profile.address;
      this.cancelFileSelection();
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
    const allowedExts = ['.jpg', '.jpeg', '.png', '.webp'];
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

    if (!allowedTypes.includes(file.type) && !allowedExts.includes(ext)) {
      this.errorMessage = 'Image must be JPG, PNG or WEBP.';
      input.value = '';
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.errorMessage = 'Image size must be less than 5 MB.';
      input.value = '';
      return;
    }

    this.errorMessage = '';
    this.selectedFile = file;
    this.selectedFileName = file.name;

    const reader = new FileReader();
    reader.onload = () => {
      this.selectedImagePreview = reader.result as string;
    };
    reader.readAsDataURL(file);
  }

  cancelFileSelection(): void {
    this.selectedFile = null;
    this.selectedFileName = '';
    this.selectedImagePreview = null;
  }

  uploadSelectedImage(): void {
    if (!this.selectedFile) return;

    this.isUploadingImage = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.profileService.uploadProfileImage(this.selectedFile).pipe(
      finalize(() => {
        this.isUploadingImage = false;
      })
    ).subscribe({
      next: (res) => {
        this.profile = res;
        this.imageTimestamp = Date.now();
        this.avatarLoadFailed = false;
        this.cancelFileSelection();
        this.authService.updateUserProfileImage(res.profileImageUrl);
        this.successMessage = 'Profile image updated successfully.';
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: (err) => {
        this.errorMessage = err?.error?.message || 'Unable to upload profile image.';
      }
    });
  }

  confirmRemoveImage(): void {
    if (!confirm('Remove your profile image?')) return;

    this.isUploadingImage = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.profileService.removeProfileImage().pipe(
      finalize(() => {
        this.isUploadingImage = false;
      })
    ).subscribe({
      next: (res) => {
        this.profile = res;
        this.imageTimestamp = Date.now();
        this.avatarLoadFailed = false;
        this.cancelFileSelection();
        this.authService.updateUserProfileImage(null);
        this.successMessage = 'Profile image removed successfully.';
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: (err) => {
        this.errorMessage = err?.error?.message || 'Unable to remove profile image.';
      }
    });
  }

  saveProfile(): void {
    if (!this.editFullName.trim()) {
      this.errorMessage = 'Full name cannot be empty.';
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.profileService.updateProfile({
      fullName: this.editFullName.trim(),
      phone: this.editPhone.trim(),
      address: this.editAddress.trim()
    }).pipe(
      finalize(() => {
        this.isSaving = false;
      })
    ).subscribe({
      next: (res) => {
        this.profile = res;
        this.isEditing = false;
        this.successMessage = 'Profile updated successfully.';
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: (err) => {
        this.errorMessage = err?.error?.message || 'Unable to update profile.';
      }
    });
  }

  getDisplayAvatarUrl(): string | null {
    if (this.avatarLoadFailed || !this.profile?.profileImageUrl) return null;
    let url = this.profile.profileImageUrl;
    if (url.startsWith('/')) {
      url = `${environment.apiUrl.replace(/\/api$/, '')}${url}`;
    }
    return `${url}?v=${this.imageTimestamp}`;
  }

  onAvatarError(): void {
    this.avatarLoadFailed = true;
  }

  getInitials(name?: string | null): string {
    if (!name) return 'C';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
  }
}
