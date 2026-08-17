import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  UserPreferenceResponse,
  UpdateUserPreferenceRequest,
  AccountSettingsResponse,
  UpdateAccountProfileRequest,
  ChangePasswordRequest
} from '../models/user-preference.models';

@Injectable({
  providedIn: 'root'
})
export class UserPreferenceService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/preferences`;

  getPreferences(): Observable<UserPreferenceResponse> {
    return this.http.get<UserPreferenceResponse>(this.apiUrl).pipe(
      timeout(10000)
    );
  }

  updatePreferences(request: UpdateUserPreferenceRequest): Observable<UserPreferenceResponse> {
    return this.http.put<UserPreferenceResponse>(this.apiUrl, request).pipe(
      timeout(10000)
    );
  }

  getAccountSettings(): Observable<AccountSettingsResponse> {
    return this.http.get<AccountSettingsResponse>(`${this.apiUrl}/account`).pipe(
      timeout(10000)
    );
  }

  updateAccountProfile(request: UpdateAccountProfileRequest): Observable<AccountSettingsResponse> {
    return this.http.put<AccountSettingsResponse>(`${this.apiUrl}/account`, request).pipe(
      timeout(10000)
    );
  }

  changePassword(request: ChangePasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/change-password`, request).pipe(
      timeout(10000)
    );
  }
}
