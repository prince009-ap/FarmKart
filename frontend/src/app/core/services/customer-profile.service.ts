import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CustomerProfileResponse, UpdateCustomerProfileRequest } from '../models/customer-profile.models';

@Injectable({
  providedIn: 'root'
})
export class CustomerProfileService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/customer/profile`;

  getProfile(): Observable<CustomerProfileResponse> {
    return this.http.get<CustomerProfileResponse>(this.apiUrl, { withCredentials: true });
  }

  updateProfile(request: UpdateCustomerProfileRequest): Observable<CustomerProfileResponse> {
    return this.http.put<CustomerProfileResponse>(this.apiUrl, request, { withCredentials: true });
  }

  uploadProfileImage(file: File): Observable<CustomerProfileResponse> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.http.post<CustomerProfileResponse>(`${this.apiUrl}/image`, formData, { withCredentials: true });
  }

  removeProfileImage(): Observable<CustomerProfileResponse> {
    return this.http.delete<CustomerProfileResponse>(`${this.apiUrl}/image`, { withCredentials: true });
  }
}
