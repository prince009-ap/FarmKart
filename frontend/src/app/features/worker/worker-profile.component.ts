import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { WorkerJobService } from './worker-job.service';
import {
  ProfileCompletionSection,
  WorkerProfile,
  WorkerProfileCompletion,
  WorkerProfileUpdateRequest,
  WorkerRatingSummary
} from '../../core/models/worker.models';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-worker-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatSnackBarModule
  ],
  templateUrl: './worker-profile.component.html'
})
export class WorkerProfileComponent implements OnInit {
  private readonly workerService = inject(WorkerJobService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  protected Math = Math;

  profile = signal<WorkerProfile | null>(null);
  profileCompletion = signal<WorkerProfileCompletion | null>(null);
  ratingSummary = signal<WorkerRatingSummary | null>(null);
  loading = signal(true);
  saving = signal(false);
  editMode = signal(false);
  loadError = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  skills = signal<string[]>([]);
  newSkillInput = signal<string>('');
  skillError = signal<string | null>(null);

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
      experienceDescription: ['', [Validators.maxLength(2000)]],
      expectedDailyWage: [0, [Validators.min(0)]],
      profileImageUrl: [''],
      isAvailable: [true],
      availableFrom: [''],
      availabilityNotes: ['', [Validators.maxLength(500)]]
    });
  }

  loadProfile(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.workerService.getProfile().subscribe({
      next: (data) => {
        this.profile.set(data);
        this.skills.set(data.skills || []);
        this.patchForm(data);
        this.loading.set(false);

        // Load Profile Completion & Ratings
        this.loadProfileCompletion();
        this.workerService.getReviews().subscribe({
          next: (revData) => this.ratingSummary.set(revData),
          error: () => {}
        });
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

  loadProfileCompletion(): void {
    this.workerService.getProfileCompletion().subscribe({
      next: (comp) => this.profileCompletion.set(comp),
      error: () => {}
    });
  }

  private patchForm(data: WorkerProfile): void {
    this.profileForm.patchValue({
      fullName: data.fullName,
      phone: data.phone,
      address: data.address,
      experienceYears: data.experienceYears,
      experienceDescription: data.experienceDescription || '',
      expectedDailyWage: data.expectedDailyWage,
      profileImageUrl: data.profileImageUrl || '',
      isAvailable: data.isAvailable ?? true,
      availableFrom: data.availableFrom || '',
      availabilityNotes: data.availabilityNotes || ''
    });
    this.skills.set(data.skills || []);
    this.newSkillInput.set('');
    this.skillError.set(null);
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

  onSectionAction(section: ProfileCompletionSection): void {
    if (section.actionRoute === '/worker/preferences') {
      this.router.navigate(['/worker/preferences']);
    } else {
      this.enableEdit();
    }
  }

  addSkill(): void {
    const name = this.newSkillInput().trim();
    this.skillError.set(null);

    if (!name) {
      this.skillError.set('Skill name cannot be empty.');
      return;
    }

    const current = this.skills();
    if (current.some(s => s.toLowerCase() === name.toLowerCase())) {
      this.skillError.set(`Skill "${name}" has already been added.`);
      return;
    }

    this.skills.set([...current, name]);
    this.newSkillInput.set('');
  }

  removeSkill(index: number): void {
    this.skills.update(list => list.filter((_, i) => i !== index));
  }

  toggleAvailability(event: any): void {
    const available = Boolean(event.checked ?? event);
    this.profileForm.patchValue({ isAvailable: available });
    if (!available) {
      this.profileForm.patchValue({ availableFrom: '' });
    }
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
      experienceDescription: val.experienceDescription ? val.experienceDescription.trim() : null,
      expectedDailyWage: Number(val.expectedDailyWage || 0),
      profileImageUrl: val.profileImageUrl ? val.profileImageUrl.trim() : null,
      skills: this.skills(),
      isAvailable: Boolean(val.isAvailable),
      availableFrom: val.isAvailable && val.availableFrom ? val.availableFrom : null,
      availabilityNotes: val.availabilityNotes ? val.availabilityNotes.trim() : null
    };

    this.workerService.updateProfile(request).subscribe({
      next: (updatedProfile) => {
        this.profile.set(updatedProfile);
        this.skills.set(updatedProfile.skills || []);
        this.saving.set(false);
        this.editMode.set(false);
        this.successMessage.set('Profile updated successfully.');
        this.loadProfileCompletion();
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
