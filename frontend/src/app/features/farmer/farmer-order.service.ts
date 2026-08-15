import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  FarmerOrderDetail,
  FarmerOrderFilter,
  FarmerOrderListItem,
  FarmerOrderSummary
} from '../../core/models/farmer-order.models';

@Injectable({
  providedIn: 'root'
})
export class FarmerOrderService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/farmer/orders`;

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

  getFarmerOrderSummary(): Observable<FarmerOrderSummary> {
    return this.http.get<FarmerOrderSummary>(`${this.baseUrl}/summary`);
  }

  getFarmerOrders(filter?: FarmerOrderFilter): Observable<FarmerOrderListItem[]> {
    let params = new HttpParams();
    if (filter?.search?.trim()) {
      params = params.set('search', filter.search.trim());
    }
    if (filter?.status?.trim()) {
      params = params.set('status', filter.status.trim());
    }
    if (filter?.sortBy?.trim()) {
      params = params.set('sortBy', filter.sortBy.trim());
    }

    return this.http.get<FarmerOrderListItem[]>(this.baseUrl, { params }).pipe(
      map(orders => orders.map(o => ({
        ...o,
        primaryImageUrl: this.resolveImageUrl(o.primaryImageUrl)
      })))
    );
  }

  getFarmerOrderById(id: string): Observable<FarmerOrderDetail> {
    return this.http.get<FarmerOrderDetail>(`${this.baseUrl}/${id}`).pipe(
      map(order => ({
        ...order,
        primaryImageUrl: this.resolveImageUrl(order.primaryImageUrl)
      }))
    );
  }
}
