import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { FarmerJob, FarmerJobRequest } from '../../core/models/farmer.models';
import { FarmerJobService } from './farmer-job.service';

@Component({
  selector: 'app-farmer-job-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatDatepickerModule
  ],
  templateUrl: './farmer-job-form.component.html'
})
export class FarmerJobFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly jobService = inject(FarmerJobService);

  jobId = this.route.snapshot.paramMap.get('id');
  loading = signal(!!this.jobId);
  saving = signal(false);
  error = signal('');

  form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    workCategory: ['', [Validators.required]],
    cropType: [''],
    workersRequired: [1, [Validators.required, Validators.min(1)]],
    requiredExperience: [0, [Validators.required, Validators.min(0)]],
    wagePerDay: [0, [Validators.required, Validators.min(0)]],
    startDate: [null as Date | null, Validators.required],
    endDate: [null as Date | null, Validators.required],
    workingHours: ['', Validators.required],
    farmLocation: ['', Validators.required],
    farmSize: [null as number | null, Validators.min(0)],
    foodProvided: [false],
    accommodationProvided: [false],
    isUrgent: [false]
  });

  ngOnInit(): void {
    if (!this.jobId) {
      return;
    }

    this.jobService.getJob(this.jobId).subscribe({
      next: job => {
        this.form.patchValue(this.toFormValue(job));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load this job.');
        this.loading.set(false);
      }
    });
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }

    const raw = this.form.getRawValue();
    const startDate = raw.startDate!;
    const endDate = raw.endDate!;

    if (endDate < startDate) {
      this.error.set('End date must be on or after the start date.');
      return;
    }

    const request: FarmerJobRequest = {
      title: raw.title!,
      description: raw.description!,
      workCategory: raw.workCategory!,
      cropType: raw.cropType?.trim() || null,
      workersRequired: Number(raw.workersRequired),
      requiredExperience: Number(raw.requiredExperience),
      wagePerDay: Number(raw.wagePerDay),
      startDate: this.toIsoDate(startDate),
      endDate: this.toIsoDate(endDate),
      workingHours: raw.workingHours!,
      farmLocation: raw.farmLocation!,
      farmSize: raw.farmSize === null ? null : Number(raw.farmSize),
      foodProvided: raw.foodProvided ?? false,
      accommodationProvided: raw.accommodationProvided ?? false,
      isUrgent: raw.isUrgent ?? false
    };

    this.saving.set(true);
    const action = this.jobId
      ? this.jobService.updateJob(this.jobId, request)
      : this.jobService.createJob(request);

    action.subscribe({
      next: job => this.router.navigate(['/farmer/jobs', job.id]),
      error: err => {
        this.error.set(err.error?.message || 'Unable to save the job.');
        this.saving.set(false);
      }
    });
  }

  private toFormValue(job: FarmerJob) {
    const { startDate, endDate, ...rest } = job;
    return {
      ...rest,
      startDate: this.parseIsoDate(startDate),
      endDate: this.parseIsoDate(endDate)
    };
  }

  private parseIsoDate(value: string): Date {
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  private toIsoDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
