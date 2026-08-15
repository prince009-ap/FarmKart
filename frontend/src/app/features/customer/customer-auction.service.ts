import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuctionBid,
  AuctionPayment,
  AuctionResult,
  CustomerAuction,
  CustomerAuctionFilter,
  CustomerMyBid,
  CustomerPaymentHistory
} from '../../core/models/customer-auction.models';

@Injectable({
  providedIn: 'root'
})
export class CustomerAuctionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/customer/auctions`;
  private readonly bidsUrl = `${environment.apiUrl}/customer/bids`;
  private readonly paymentsUrl = `${environment.apiUrl}/customer/payments`;

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

  getMarketplaceAuctions(filter?: CustomerAuctionFilter): Observable<CustomerAuction[]> {
    let params = new HttpParams();
    if (filter?.search) params = params.set('search', filter.search);
    if (filter?.category) params = params.set('category', filter.category);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.location) params = params.set('location', filter.location);
    if (filter?.sortBy) params = params.set('sortBy', filter.sortBy);

    return this.http.get<CustomerAuction[]>(this.apiUrl, { params }).pipe(
      map(auctions => auctions.map(a => ({
        ...a,
        primaryImageUrl: this.resolveImageUrl(a.primaryImageUrl),
        images: (a.images || []).map(img => this.resolveImageUrl(img) || img)
      })))
    );
  }

  getAuctionById(id: string): Observable<CustomerAuction> {
    return this.http.get<CustomerAuction>(`${this.apiUrl}/${id}`).pipe(
      map(auction => ({
        ...auction,
        primaryImageUrl: this.resolveImageUrl(auction.primaryImageUrl),
        images: (auction.images || []).map(img => this.resolveImageUrl(img) || img)
      }))
    );
  }

  placeBid(auctionId: string, amount: number, requestedQuantityKg: number): Observable<AuctionBid> {
    return this.http.post<AuctionBid>(`${this.apiUrl}/${auctionId}/bids`, { amount, requestedQuantityKg });
  }

  getAuctionBids(auctionId: string, sortBy?: string): Observable<AuctionBid[]> {
    let params = new HttpParams();
    if (sortBy) params = params.set('sortBy', sortBy);
    return this.http.get<AuctionBid[]>(`${this.apiUrl}/${auctionId}/bids`, { params });
  }

  getAuctionResult(id: string): Observable<AuctionResult> {
    return this.http.get<AuctionResult>(`${this.apiUrl}/${id}/result`);
  }

  getMyBids(): Observable<CustomerMyBid[]> {
    return this.http.get<CustomerMyBid[]>(this.bidsUrl).pipe(
      map(bids => bids.map(bid => ({
        ...bid,
        primaryImageUrl: this.resolveImageUrl(bid.primaryImageUrl)
      })))
    );
  }

  processAuctionPayment(
    auctionId: string,
    paymentMethod: string,
    fulfillmentDetails?: {
      fulfillmentMode?: string;
      deliveryAddress?: string;
      deliveryCity?: string;
      deliveryState?: string;
      deliveryPincode?: string;
      contactName?: string;
      contactPhone?: string;
      pickupDate?: string;
    }
  ): Observable<AuctionPayment> {
    const body = {
      paymentMethod,
      ...fulfillmentDetails
    };
    return this.http.post<AuctionPayment>(`${this.apiUrl}/${auctionId}/payments`, body);
  }

  getPaymentHistory(): Observable<CustomerPaymentHistory[]> {
    return this.http.get<CustomerPaymentHistory[]>(this.paymentsUrl).pipe(
      map(payments => payments.map(p => ({
        ...p,
        primaryImageUrl: this.resolveImageUrl(p.primaryImageUrl)
      })))
    );
  }
}
