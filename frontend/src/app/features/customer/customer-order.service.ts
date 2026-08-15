import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CustomerOrderDetail,
  CustomerOrderFilter,
  CustomerOrderListItem,
  CustomerOrderTracking,
  UpdateFulfillmentDetailsRequest,
  UpdateOrderStatusRequest
} from '../../core/models/customer-auction.models';

@Injectable({
  providedIn: 'root'
})
export class CustomerOrderService {
  private readonly http = inject(HttpClient);
  private readonly ordersUrl = `${environment.apiUrl}/customer/orders`;

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

  getCustomerOrders(filter?: CustomerOrderFilter): Observable<CustomerOrderListItem[]> {
    let params = new HttpParams();
    if (filter?.search) params = params.set('search', filter.search);
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.sortBy) params = params.set('sortBy', filter.sortBy);

    return this.http.get<CustomerOrderListItem[]>(this.ordersUrl, { params }).pipe(
      map(orders => orders.map(o => ({
        ...o,
        primaryImageUrl: this.resolveImageUrl(o.primaryImageUrl)
      })))
    );
  }

  getCustomerOrderById(id: string): Observable<CustomerOrderDetail> {
    return this.http.get<CustomerOrderDetail>(`${this.ordersUrl}/${id}`).pipe(
      map(order => ({
        ...order,
        primaryImageUrl: this.resolveImageUrl(order.primaryImageUrl)
      }))
    );
  }

  updateOrderStatus(orderId: string, newStatus: string, note?: string): Observable<any> {
    const payload: UpdateOrderStatusRequest = { newStatus, note };
    return this.http.patch(`${this.ordersUrl}/${orderId}/status`, payload);
  }

  updateFulfillmentDetails(orderId: string, request: UpdateFulfillmentDetailsRequest): Observable<CustomerOrderDetail> {
    return this.http.put<CustomerOrderDetail>(`${this.ordersUrl}/${orderId}/fulfillment`, request).pipe(
      map(order => ({
        ...order,
        primaryImageUrl: this.resolveImageUrl(order.primaryImageUrl)
      }))
    );
  }

  getOrderTracking(id: string): Observable<CustomerOrderTracking> {
    return this.http.get<CustomerOrderTracking>(`${this.ordersUrl}/${id}/tracking`).pipe(
      map(tracking => ({
        ...tracking,
        primaryImageUrl: this.resolveImageUrl(tracking.primaryImageUrl)
      }))
    );
  }
}
