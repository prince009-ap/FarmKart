import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddWishlistItemRequest,
  WishlistCountResponse,
  WishlistItemResponse,
  WishlistItemType,
  WishlistStatusResponse
} from '../models/wishlist.models';

@Injectable({
  providedIn: 'root'
})
export class WishlistService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/customer/wishlist`;

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

  getWishlist(itemType?: WishlistItemType): Observable<WishlistItemResponse[]> {
    let params = new HttpParams();
    if (itemType) {
      params = params.set('itemType', itemType);
    }
    return this.http.get<WishlistItemResponse[]>(this.apiUrl, { params }).pipe(
      map(items => items.map(item => ({
        ...item,
        primaryImageUrl: this.resolveImageUrl(item.primaryImageUrl)
      })))
    );
  }

  getCount(): Observable<WishlistCountResponse> {
    return this.http.get<WishlistCountResponse>(`${this.apiUrl}/count`);
  }

  getItemStatus(itemType: WishlistItemType, itemId: string): Observable<WishlistStatusResponse> {
    return this.http.get<WishlistStatusResponse>(`${this.apiUrl}/${itemType}/${itemId}/status`);
  }

  addItem(request: AddWishlistItemRequest): Observable<WishlistItemResponse> {
    return this.http.post<WishlistItemResponse>(this.apiUrl, request);
  }

  removeItem(itemType: WishlistItemType, itemId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${itemType}/${itemId}`);
  }
}
