import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCardModule } from '@angular/material/card';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
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
  templateUrl: './login.component.html'
})
export class LoginComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  hidePassword = true;
  loading = false;
  errorMessage = '';
  private authSubscription?: Subscription;

  readonly loginForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  ngOnInit(): void {
    // If user is already authenticated, redirect them directly
    this.authSubscription = this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.redirectToDashboard(user.role);
      }
    });
  }

  ngOnDestroy(): void {
    this.authSubscription?.unsubscribe();
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    const val = this.loginForm.value;
    this.authService.login({ email: val.email!, password: val.password! }).subscribe({
      next: (response) => {
        this.loading = false;
        // User state will update and trigger subscription in ngOnInit, but we can also trigger direct redirect
        const returnUrl = this.route.snapshot.queryParams['returnUrl'];
        if (returnUrl) {
          this.router.navigateByUrl(returnUrl);
        } else {
          this.redirectToDashboard(response.role);
        }
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.message || 'Invalid email or password. Please try again.';
      }
    });
  }

  private redirectToDashboard(role: string): void {
    if (role === 'Farmer') {
      this.router.navigate(['/farmer']);
    } else if (role === 'Worker') {
      this.router.navigate(['/worker']);
    } else if (role === 'Customer') {
      this.router.navigate(['/customer']);
    } else {
      this.router.navigate(['/']);
    }
  }
}
