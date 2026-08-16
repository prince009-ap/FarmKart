import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FarmerPublicProfileResponse } from '../models/farmer-profile.models';

@Injectable({
  providedIn: 'root'
})
export class FarmerProfileService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/farmers`;

  getPublicProfile(farmerId: string): Observable<FarmerPublicProfileResponse> {
    return this.http.get<FarmerPublicProfileResponse>(`${this.apiUrl}/${farmerId}/profile`);
  }
}
