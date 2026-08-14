import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { WorkerJobService } from './worker-job.service';
import { WorkerPreferences, WorkerPreferencesUpdateRequest } from '../../core/models/worker.models';

@Component({
  selector: 'app-worker-preferences',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule
  ],
  templateUrl: './worker-preferences.component.html'
})
export class WorkerPreferencesComponent implements OnInit {
  private readonly workerService = inject(WorkerJobService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  preferences = signal<WorkerPreferences | null>(null);
  loading = signal(true);
  saving = signal(false);
  loadError = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  categories = signal<string[]>([]);
  newCategoryInput = signal<string>('');
  categoryError = signal<string | null>(null);

  locations = signal<string[]>([]);
  newLocationInput = signal<string>('');
  locationError = signal<string | null>(null);

  prefForm!: FormGroup;

  ngOnInit(): void {
    this.buildForm();
    this.loadPreferences();
  }

  private buildForm(): void {
    this.prefForm = this.fb.group({
      minimumDailyWage: [0, [Validators.required, Validators.min(0), Validators.max(100000)]],
      preferredWorkingHours: ['08:00 AM - 05:00 PM', [Validators.maxLength(100)]],
      foodPreference: ['Preferred'],
      accommodationPreference: ['Not Required']
    });
  }

  loadPreferences(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.workerService.getPreferences().subscribe({
      next: (data) => {
        this.preferences.set(data);
        this.categories.set(data.preferredWorkCategories || []);
        this.locations.set(data.preferredLocations || []);
        this.prefForm.patchValue({
          minimumDailyWage: data.minimumDailyWage ?? 0,
          preferredWorkingHours: data.preferredWorkingHours || '08:00 AM - 05:00 PM',
          foodPreference: data.foodPreference || 'Preferred',
          accommodationPreference: data.accommodationPreference || 'Not Required'
        });
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.loadError.set('Failed to load job preferences. Please try again.');
      }
    });
  }

  addCategory(): void {
    const val = this.newCategoryInput().trim();
    this.categoryError.set(null);

    if (!val) {
      this.categoryError.set('Category name cannot be empty.');
      return;
    }

    const current = this.categories();
    if (current.some(c => c.toLowerCase() === val.toLowerCase())) {
      this.categoryError.set(`Category "${val}" is already added.`);
      return;
    }

    this.categories.set([...current, val]);
    this.newCategoryInput.set('');
  }

  removeCategory(index: number): void {
    this.categories.update(list => list.filter((_, i) => i !== index));
  }

  addLocation(): void {
    const val = this.newLocationInput().trim();
    this.locationError.set(null);

    if (!val) {
      this.locationError.set('Location name cannot be empty.');
      return;
    }

    const current = this.locations();
    if (current.some(l => l.toLowerCase() === val.toLowerCase())) {
      this.locationError.set(`Location "${val}" is already added.`);
      return;
    }

    this.locations.set([...current, val]);
    this.newLocationInput.set('');
  }

  removeLocation(index: number): void {
    this.locations.update(list => list.filter((_, i) => i !== index));
  }

  onSubmit(): void {
    if (this.prefForm.invalid) {
      this.prefForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.successMessage.set(null);

    const val = this.prefForm.value;
    const request: WorkerPreferencesUpdateRequest = {
      preferredWorkCategories: this.categories(),
      preferredLocations: this.locations(),
      minimumDailyWage: Number(val.minimumDailyWage || 0),
      preferredWorkingHours: val.preferredWorkingHours ? val.preferredWorkingHours.trim() : null,
      foodPreference: val.foodPreference ? val.foodPreference.trim() : null,
      accommodationPreference: val.accommodationPreference ? val.accommodationPreference.trim() : null
    };

    this.workerService.updatePreferences(request).subscribe({
      next: (updated) => {
        this.preferences.set(updated);
        this.categories.set(updated.preferredWorkCategories || []);
        this.locations.set(updated.preferredLocations || []);
        this.saving.set(false);
        this.successMessage.set('Preferences saved successfully.');
        this.snackBar.open('Job preferences updated successfully!', 'Close', { duration: 4000 });
      },
      error: (err) => {
        this.saving.set(false);
        const msg = err.error?.message || 'Failed to save preferences. Please try again.';
        this.snackBar.open(msg, 'Close', { duration: 5000 });
      }
    });
  }
}
