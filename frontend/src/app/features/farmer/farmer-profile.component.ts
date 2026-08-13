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
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  readonly farmSizeUnitOptions: FarmSizeUnit[] = ['Vigha', 'Acre', 'Hectare'];

  profile = signal<FarmerProfile | null>(null);
  loading = signal(true);
  saving = signal(false);
  editMode = signal(false);
  loadError = signal<string | null>(null);

  profileForm!: FormGroup;

  ngOnInit(): void {
    this.buildForm();
    this.loadProfile();
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
}
