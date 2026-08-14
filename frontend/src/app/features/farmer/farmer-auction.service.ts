import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateFarmerAuctionRequest, FarmerAuction } from '../../core/models/farmer-crop.models';

@Injectable({ providedIn: 'root' })
export class FarmerAuctionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/farmer/auctions`;
  getAuctions(): Observable<FarmerAuction[]> { return this.http.get<FarmerAuction[]>(this.apiUrl); }
  createAuction(request: CreateFarmerAuctionRequest): Observable<FarmerAuction> { return this.http.post<FarmerAuction>(this.apiUrl, request); }
  cancelAuction(id: string): Observable<void> { return this.http.post<void>(`${this.apiUrl}/${id}/cancel`, {}); }
}
