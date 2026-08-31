import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCardModule } from '@angular/material/card';
import { CommonModule } from '@angular/common';
import { passwordMatchValidator } from './password-match.validator';

@Component({
  selector: 'app-register-worker',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatCardModule
  ],
  templateUrl: './register-worker.component.html'
})
export class RegisterWorkerComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  hidePassword = true;
  hideConfirmPassword = true;
  loading = false;
  errorMessage = '';
  successMessage = '';

  readonly registerForm = this.fb.group({
    fullName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [
      Validators.required,
      Validators.minLength(6),
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$/)
    ]],
    confirmPassword: ['', [Validators.required]],
    phone: ['', [Validators.required, Validators.pattern(/^[0-9+() -]{10,15}$/)]],
    address: ['', [Validators.required]],
    experienceYears: [null as number | null, [Validators.required, Validators.min(0)]],
    expectedDailyWage: [null as number | null, [Validators.required, Validators.min(0)]]
  }, { validators: passwordMatchValidator });

  onSubmit(): void {
    if (this.registerForm.invalid) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    const val = this.registerForm.value;
    const request = {
      fullName: val.fullName!,
      email: val.email!,
      password: val.password!,
      phone: val.phone!,
      profileImageUrl: null,
      address: val.address!,
      experienceYears: Number(val.experienceYears),
      expectedDailyWage: Number(val.expectedDailyWage)
    };

    this.authService.registerWorker(request).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = 'Worker registered successfully! Redirecting to login...';
        setTimeout(() => this.router.navigate(['/auth/login']), 2000);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.message || 'An error occurred during registration. Please try again.';
      }
    });
  }
}
