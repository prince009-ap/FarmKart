import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  CreateFarmerAuctionRequest,
  FarmerAuction,
  FarmerAuctionBid,
  FarmerAuctionSummaryCounts
} from '../../core/models/farmer-crop.models';

@Injectable({ providedIn: 'root' })
export class FarmerAuctionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/farmer/auctions`;

  private get serverBaseUrl(): string {
    return environment.apiUrl.replace(/\/api\/?$/, '');
  }

  resolveImageUrl(url: string | null | undefined): string | null {
    if (!url) return null;
    if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) {
      return url;
    }
    const cleanPath = url.startsWith('/') ? url : `/${url}`;
    return `${this.serverBaseUrl}${cleanPath}`;
  }

  private transformAuction(auction: FarmerAuction): FarmerAuction {
    return {
      ...auction,
      primaryImageUrl: this.resolveImageUrl(auction.primaryImageUrl)
    };
  }

  getAuctions(): Observable<FarmerAuction[]> {
    return this.http.get<FarmerAuction[]>(this.apiUrl).pipe(
      map(auctions => auctions.map(a => this.transformAuction(a)))
    );
  }

  getSummaryCounts(): Observable<FarmerAuctionSummaryCounts> {
    return this.http.get<FarmerAuctionSummaryCounts>(`${this.apiUrl}/summary`);
  }

  getAuction(id: string): Observable<FarmerAuction> {
    return this.http.get<FarmerAuction>(`${this.apiUrl}/${id}`).pipe(
      map(a => this.transformAuction(a))
    );
  }

  getAuctionBids(id: string, sortBy?: string): Observable<FarmerAuctionBid[]> {
    let params = new HttpParams();
    if (sortBy) {
      params = params.set('sortBy', sortBy);
    }
    return this.http.get<FarmerAuctionBid[]>(`${this.apiUrl}/${id}/bids`, { params });
  }

  getAuctionResult(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${id}/result`);
  }

  getAuctionPayment(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${id}/payment`);
  }

  createAuction(request: CreateFarmerAuctionRequest): Observable<FarmerAuction> {
    return this.http.post<FarmerAuction>(this.apiUrl, request);
  }

  cancelAuction(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/cancel`, {});
  }
}
