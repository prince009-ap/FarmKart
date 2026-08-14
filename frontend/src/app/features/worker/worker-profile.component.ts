import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { WorkerJobService } from './worker-job.service';
import { WorkerProfile, WorkerProfileUpdateRequest } from '../../core/models/worker.models';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-worker-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './worker-profile.component.html'
})
export class WorkerProfileComponent implements OnInit {
  private readonly workerService = inject(WorkerJobService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  profile = signal<WorkerProfile | null>(null);
  loading = signal(true);
  saving = signal(false);
  editMode = signal(false);
  loadError = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  profileForm!: FormGroup;

  ngOnInit(): void {
    this.buildForm();
    this.loadProfile();
  }

  private buildForm(): void {
    this.profileForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(150)]],
      phone: ['', [Validators.required, Validators.pattern(/^\+?[0-9\s\-]{7,20}$/)]],
      address: ['', [Validators.required]],
      experienceYears: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
      expectedDailyWage: [0, [Validators.min(0)]],
      profileImageUrl: ['']
    });
  }

  loadProfile(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.workerService.getProfile().subscribe({
      next: (data) => {
        this.profile.set(data);
        this.patchForm(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 404) {
          this.loadError.set('Worker profile not found.');
        } else if (err.status === 401) {
          this.loadError.set('You are not authenticated. Please log in again.');
        } else if (err.status === 403) {
          this.loadError.set('You do not have permission to view this profile.');
        } else {
          this.loadError.set('Failed to load profile. Please try again.');
        }
      }
    });
  }

  private patchForm(data: WorkerProfile): void {
    this.profileForm.patchValue({
      fullName: data.fullName,
      phone: data.phone,
      address: data.address,
      experienceYears: data.experienceYears,
      expectedDailyWage: data.expectedDailyWage,
      profileImageUrl: data.profileImageUrl || ''
    });
  }

  enableEdit(): void {
    const current = this.profile();
    if (current) {
      this.patchForm(current);
    }
    this.successMessage.set(null);
    this.editMode.set(true);
  }

  cancelEdit(): void {
    const current = this.profile();
    if (current) {
      this.patchForm(current);
    }
    this.editMode.set(false);
    this.successMessage.set(null);
  }

  onSubmit(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.successMessage.set(null);

    const val = this.profileForm.value;
    const request: WorkerProfileUpdateRequest = {
      fullName: val.fullName.trim(),
      phone: val.phone.trim(),
      address: val.address.trim(),
      experienceYears: Number(val.experienceYears),
      expectedDailyWage: Number(val.expectedDailyWage || 0),
      profileImageUrl: val.profileImageUrl ? val.profileImageUrl.trim() : null
    };

    this.workerService.updateProfile(request).subscribe({
      next: (updatedProfile) => {
        this.profile.set(updatedProfile);
        this.saving.set(false);
        this.editMode.set(false);
        this.successMessage.set('Profile updated successfully.');
        this.snackBar.open('Profile updated successfully!', 'Close', { duration: 4000 });
      },
      error: (err) => {
        this.saving.set(false);
        const msg = err.error?.message || 'Failed to update profile. Please try again.';
        this.snackBar.open(msg, 'Close', { duration: 5000 });
      }
    });
  }
}
