import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FarmerProfile, FarmerProfileUpdateRequest } from '../../core/models/farmer.models';

@Injectable({
  providedIn: 'root'
})
export class FarmerProfileService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/farmer`;

  /**
   * GET /api/farmer/profile
   * Returns the currently authenticated farmer's own profile.
   * Credentials are forwarded automatically by the global authInterceptor.
   */
  getProfile(): Observable<FarmerProfile> {
    return this.http.get<FarmerProfile>(`${this.apiUrl}/profile`);
  }

  /**
   * PUT /api/farmer/profile
   * Updates the currently authenticated farmer's own profile.
   * The backend resolves ownership from the JWT cookie — userId is never sent from the client.
   */
  updateProfile(request: FarmerProfileUpdateRequest): Observable<FarmerProfile> {
    return this.http.put<FarmerProfile>(`${this.apiUrl}/profile`, request);
  }

  uploadProfileImage(file: File): Observable<FarmerProfile> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<FarmerProfile>(`${this.apiUrl}/profile/image`, formData);
  }

  removeProfileImage(): Observable<FarmerProfile> {
    return this.http.delete<FarmerProfile>(`${this.apiUrl}/profile/image`);
  }
}
