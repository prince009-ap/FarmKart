import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { finalize, forkJoin, of, catchError } from 'rxjs';
import { UserPreferenceService } from '../../core/services/user-preference.service';
import { AuthService } from '../../core/services/auth.service';
import {
  UserPreferenceResponse,
  AccountSettingsResponse,
  UpdateUserPreferenceRequest,
  UpdateAccountProfileRequest,
  ChangePasswordRequest
} from '../../core/models/user-preference.models';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen bg-slate-50 py-8 px-4 sm:px-6 lg:px-8">
      <div class="max-w-4xl mx-auto space-y-8">
        
        <!-- Header -->
        <div class="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-slate-200/80 flex items-center justify-between gap-4">
          <div class="flex items-center gap-4">
            <div class="w-12 h-12 rounded-2xl bg-emerald-100 text-emerald-700 flex items-center justify-center text-2xl font-bold">
              ⚙️
            </div>
            <div>
              <h1 class="text-2xl font-bold text-slate-900">Account & Settings</h1>
              <p class="text-xs text-slate-500 mt-1">Manage your account information, app preferences, and security options.</p>
            </div>
          </div>
          <button (click)="logout()" class="px-4 py-2 text-xs font-semibold text-red-600 bg-red-50 hover:bg-red-100 rounded-xl transition flex items-center gap-2">
            <span>Logout</span>
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
            </svg>
          </button>
        </div>

        <!-- Global Messages -->
        <div *ngIf="successMessage" class="bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs rounded-2xl p-4 flex items-center gap-3">
          <span class="text-lg">✅</span>
          <p class="font-medium">{{ successMessage }}</p>
        </div>

        <div *ngIf="errorMessage" class="bg-red-50 border border-red-200 text-red-800 text-xs rounded-2xl p-4 flex items-center justify-between gap-3">
          <div class="flex items-center gap-3">
            <span class="text-lg">⚠️</span>
            <p class="font-medium">{{ errorMessage }}</p>
          </div>
          <button (click)="loadAllData()" class="px-3 py-1 text-[11px] font-bold text-red-700 bg-red-100 hover:bg-red-200 rounded-lg transition">
            Retry
          </button>
        </div>

        <div class="space-y-8">

          <!-- 1. ACCOUNT INFORMATION -->
          <div class="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-slate-200/80 space-y-6">
            <div class="flex items-center justify-between pb-4 border-b border-slate-100">
              <div class="flex items-center gap-3">
                <span class="text-xl">👤</span>
                <h2 class="text-lg font-bold text-slate-900">Account Information</h2>
              </div>
              <button *ngIf="!isEditingAccount" (click)="toggleEditAccount()" class="px-3.5 py-1.5 text-xs font-semibold text-emerald-700 bg-emerald-50 hover:bg-emerald-100 rounded-xl transition">
                Edit Profile
              </button>
            </div>

            <!-- Read View -->
            <div *ngIf="!isEditingAccount" class="grid grid-cols-1 sm:grid-cols-2 gap-6">
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Full Name</label>
                <p class="text-sm font-semibold text-slate-800">{{ account?.fullName || 'Not provided' }}</p>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Email Address</label>
                <p class="text-sm font-semibold text-slate-800">{{ account?.email }}</p>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Account Role</label>
                <span class="inline-flex items-center px-2.5 py-1 text-xs font-bold text-emerald-800 bg-emerald-100 rounded-full">
                  {{ account?.role }}
                </span>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Phone Number</label>
                <p class="text-sm font-semibold text-slate-800">{{ account?.phone || 'Not provided' }}</p>
              </div>
            </div>

            <!-- Edit View -->
            <form *ngIf="isEditingAccount" (ngSubmit)="saveAccountProfile()" class="space-y-4 max-w-lg">
              <div>
                <label for="settings-fullname" class="block text-xs font-semibold text-slate-700 mb-1">Full Name</label>
                <input id="settings-fullname" name="settings-fullname" type="text" [(ngModel)]="editFullName" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50" required />
              </div>
              <div>
                <label for="settings-phone" class="block text-xs font-semibold text-slate-700 mb-1">Phone Number</label>
                <input id="settings-phone" name="settings-phone" type="text" [(ngModel)]="editPhone" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50" />
              </div>
              <div class="flex items-center gap-3 pt-2">
                <button type="submit" [disabled]="isSavingAccount" class="px-4 py-2 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 rounded-xl transition">
                  {{ isSavingAccount ? 'Saving...' : 'Save Profile' }}
                </button>
                <button type="button" (click)="toggleEditAccount()" class="px-4 py-2 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl transition">
                  Cancel
                </button>
              </div>
            </form>
          </div>

          <!-- 2. USER PREFERENCES -->
          <div class="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-slate-200/80 space-y-6">
            <div class="flex items-center justify-between pb-4 border-b border-slate-100">
              <div class="flex items-center gap-3">
                <span class="text-xl">🎨</span>
                <h2 class="text-lg font-bold text-slate-900">Application Preferences</h2>
              </div>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-6">
              <!-- Theme -->
              <div>
                <label for="settings-theme" class="block text-xs font-semibold text-slate-700 mb-2">Display Theme</label>
                <select id="settings-theme" name="settings-theme" [(ngModel)]="prefForm.theme" class="w-full text-xs rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50">
                  <option value="light">☀️ Light Theme</option>
                  <option value="dark">🌙 Dark Theme</option>
                  <option value="system">🖥️ System Default</option>
                </select>
              </div>

              <!-- Language -->
              <div>
                <label for="settings-language" class="block text-xs font-semibold text-slate-700 mb-2">Preferred Language</label>
                <select id="settings-language" name="settings-language" [(ngModel)]="prefForm.language" class="w-full text-xs rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50">
                  <option value="en">English (US)</option>
                  <option value="hi">Hindi (हिन्दी)</option>
                  <option value="gu">Gujarati (ગુજરાતી)</option>
                </select>
              </div>
            </div>

            <!-- Toggles -->
            <div class="space-y-4 pt-2">
              <label class="flex items-center justify-between p-3 rounded-2xl bg-slate-50 border border-slate-200/60 cursor-pointer hover:bg-slate-100/60 transition">
                <div>
                  <span class="block text-xs font-semibold text-slate-800">Email Notifications</span>
                  <span class="block text-[11px] text-slate-500">Receive email alerts for order updates, auctions, and rental statuses.</span>
                </div>
                <input type="checkbox" [(ngModel)]="prefForm.emailAlerts" class="w-4 h-4 text-emerald-600 rounded border-slate-300 focus:ring-emerald-500" />
              </label>

              <label class="flex items-center justify-between p-3 rounded-2xl bg-slate-50 border border-slate-200/60 cursor-pointer hover:bg-slate-100/60 transition">
                <div>
                  <span class="block text-xs font-semibold text-slate-800">SMS Alerts</span>
                  <span class="block text-[11px] text-slate-500">Receive text notifications for critical delivery updates.</span>
                </div>
                <input type="checkbox" [(ngModel)]="prefForm.smsAlerts" class="w-4 h-4 text-emerald-600 rounded border-slate-300 focus:ring-emerald-500" />
              </label>

              <label class="flex items-center justify-between p-3 rounded-2xl bg-slate-50 border border-slate-200/60 cursor-pointer hover:bg-slate-100/60 transition">
                <div>
                  <span class="block text-xs font-semibold text-slate-800">Compact Dashboard Layout</span>
                  <span class="block text-[11px] text-slate-500">Enable high-density layout mode across analytics and management screens.</span>
                </div>
                <input type="checkbox" [(ngModel)]="prefForm.compactView" class="w-4 h-4 text-emerald-600 rounded border-slate-300 focus:ring-emerald-500" />
              </label>
            </div>

            <div class="pt-2">
              <button (click)="savePreferences()" [disabled]="isSavingPref" class="px-5 py-2.5 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 rounded-xl transition shadow-sm">
                {{ isSavingPref ? 'Saving Settings...' : 'Save Changes' }}
              </button>
            </div>
          </div>

          <!-- 3. SECURITY SECTION (CHANGE PASSWORD) -->
          <div class="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-slate-200/80 space-y-6">
            <div class="flex items-center gap-3 pb-4 border-b border-slate-100">
              <span class="text-xl">🔒</span>
              <div>
                <h2 class="text-lg font-bold text-slate-900">Security & Password</h2>
                <p class="text-xs text-slate-500">Ensure your account uses a strong password.</p>
              </div>
            </div>

            <form (ngSubmit)="changePassword()" class="space-y-4 max-w-lg">
              <div>
                <label for="current-password" class="block text-xs font-semibold text-slate-700 mb-1">Current Password</label>
                <input id="current-password" name="current-password" type="password" [(ngModel)]="pwdForm.currentPassword" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50" required />
              </div>

              <div>
                <label for="new-password" class="block text-xs font-semibold text-slate-700 mb-1">New Password</label>
                <input id="new-password" name="new-password" type="password" [(ngModel)]="pwdForm.newPassword" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50" required />
                <p class="text-[11px] text-slate-400 mt-1">Must be at least 8 characters with uppercase, lowercase, digit, and special character.</p>
              </div>

              <div>
                <label for="confirm-password" class="block text-xs font-semibold text-slate-700 mb-1">Confirm New Password</label>
                <input id="confirm-password" name="confirm-password" type="password" [(ngModel)]="pwdForm.confirmPassword" class="w-full text-sm rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2.5 bg-slate-50" required />
              </div>

              <div *ngIf="pwdError" class="p-3 text-xs text-red-700 bg-red-50 rounded-xl border border-red-200">
                {{ pwdError }}
              </div>

              <div *ngIf="pwdSuccess" class="p-3 text-xs text-emerald-700 bg-emerald-50 rounded-xl border border-emerald-200">
                {{ pwdSuccess }}
              </div>

              <div class="pt-2">
                <button type="submit" [disabled]="isChangingPwd" class="px-5 py-2.5 text-xs font-semibold text-white bg-slate-900 hover:bg-slate-800 disabled:opacity-50 rounded-xl transition shadow-sm">
                  {{ isChangingPwd ? 'Updating Password...' : 'Change Password' }}
                </button>
              </div>
            </form>
          </div>

          <!-- 4. DANGER ZONE -->
          <div class="bg-red-50/40 rounded-3xl p-6 sm:p-8 border border-red-200/80 space-y-4">
            <div class="flex items-center gap-3">
              <span class="text-xl">⚠️</span>
              <div>
                <h2 class="text-lg font-bold text-red-900">Danger Zone</h2>
                <p class="text-xs text-red-700">Account management & safety controls.</p>
              </div>
            </div>

            <div class="bg-white p-4 rounded-2xl border border-red-200 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
              <div>
                <h3 class="text-xs font-bold text-slate-900">Account Deactivation & Safety</h3>
                <p class="text-[11px] text-slate-500 mt-0.5">Historical business records (Auctions, Orders, Machinery Rentals, Invoices) are retained for legal audit compliance.</p>
              </div>
              <button (click)="openDangerNotice()" class="px-4 py-2 text-xs font-semibold text-red-700 bg-red-100 hover:bg-red-200 rounded-xl transition shrink-0">
                Deactivate Account
              </button>
            </div>
          </div>

        </div>
      </div>
    </div>
  `
})
export class SettingsComponent implements OnInit {
  private readonly preferenceService = inject(UserPreferenceService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  isLoading = false;
  isSavingPref = false;
  isSavingAccount = false;
  isChangingPwd = false;

  successMessage = '';
  errorMessage = '';

  account: AccountSettingsResponse | null = null;
  isEditingAccount = false;
  editFullName = '';
  editPhone = '';

  prefForm: UpdateUserPreferenceRequest = {
    theme: 'light',
    language: 'en',
    emailAlerts: true,
    smsAlerts: false,
    compactView: false
  };

  pwdForm: ChangePasswordRequest = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };
  pwdError = '';
  pwdSuccess = '';

  ngOnInit(): void {
    const user = this.authService.currentUserValue;
    if (user) {
      this.account = {
        userId: user.userId,
        fullName: user.fullName || '',
        email: user.email || '',
        role: user.role || '',
        phone: ''
      };
      this.editFullName = user.fullName || '';
    }
    this.loadAllData();
  }

  loadAllData(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';

    forkJoin({
      account: this.preferenceService.getAccountSettings().pipe(
        catchError((err) => {
          console.error('Error fetching account settings:', err);
          return of(null);
        })
      ),
      pref: this.preferenceService.getPreferences().pipe(
        catchError((err) => {
          console.error('Error fetching preferences:', err);
          return of(null);
        })
      )
    }).pipe(
      finalize(() => {
        this.isLoading = false;
      })
    ).subscribe({
      next: ({ account, pref }) => {
        if (account) {
          this.account = account;
          this.editFullName = account.fullName;
          this.editPhone = account.phone;
        } else {
          this.errorMessage = 'Unable to load account settings.';
        }

        if (pref) {
          this.prefForm = {
            theme: pref.theme || 'light',
            language: pref.language || 'en',
            emailAlerts: pref.emailAlerts ?? true,
            smsAlerts: pref.smsAlerts ?? false,
            compactView: pref.compactView ?? false
          };
        }
      }
    });
  }

  toggleEditAccount(): void {
    this.isEditingAccount = !this.isEditingAccount;
    if (this.account) {
      this.editFullName = this.account.fullName;
      this.editPhone = this.account.phone;
    }
  }

  saveAccountProfile(): void {
    if (!this.editFullName.trim()) {
      this.errorMessage = 'Full name cannot be empty.';
      return;
    }

    this.isSavingAccount = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.preferenceService.updateAccountProfile({
      fullName: this.editFullName.trim(),
      phone: this.editPhone.trim()
    }).pipe(
      finalize(() => {
        this.isSavingAccount = false;
      })
    ).subscribe({
      next: (updatedAcc) => {
        this.account = updatedAcc;
        this.isEditingAccount = false;
        this.successMessage = 'Profile information updated successfully.';
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: (err) => {
        this.errorMessage = err?.error?.message || 'Unable to update profile.';
      }
    });
  }

  savePreferences(): void {
    this.isSavingPref = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.preferenceService.updatePreferences(this.prefForm).pipe(
      finalize(() => {
        this.isSavingPref = false;
      })
    ).subscribe({
      next: (res) => {
        this.prefForm = { ...res };
        this.successMessage = 'Settings saved successfully.';
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: () => {
        this.errorMessage = 'Unable to save settings. Please try again.';
      }
    });
  }

  changePassword(): void {
    this.pwdError = '';
    this.pwdSuccess = '';

    if (!this.pwdForm.currentPassword) {
      this.pwdError = 'Current password is required.';
      return;
    }
    if (!this.pwdForm.newPassword) {
      this.pwdError = 'New password is required.';
      return;
    }
    if (this.pwdForm.newPassword !== this.pwdForm.confirmPassword) {
      this.pwdError = 'New password and confirmation do not match.';
      return;
    }

    this.isChangingPwd = true;
    this.preferenceService.changePassword(this.pwdForm).pipe(
      finalize(() => {
        this.isChangingPwd = false;
      })
    ).subscribe({
      next: (res) => {
        this.pwdSuccess = res.message || 'Password changed successfully.';
        this.pwdForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
      },
      error: (err) => {
        this.pwdError = err?.error?.message || 'Failed to change password. Verify your current password.';
      }
    });
  }

  openDangerNotice(): void {
    alert('Account Deactivation Safety Notice:\n\nIn accordance with FarmKart financial and agricultural marketplace compliance policies, your historical records (Auctions, Orders, Machinery Rentals, and Invoices) remain preserved in the audit log.\n\nTo request full account deactivation, please contact support@farmkart.com.');
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }
}
