import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  MachineryFilterRequest,
  PagedMachineryResponse,
  MachineryResponse,
  CreateMachineryRequest,
  UpdateMachineryRequest,
  MachineryImageResponse,
  BookRentalRequest,
  MachineryRentalResponse,
  UpdateRentalStatusRequest,
  MachineryAvailabilityResponse
} from '../models/machinery.models';

@Injectable({
  providedIn: 'root'
})
export class MachineryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  private get serverBaseUrl(): string {
    return this.baseUrl.replace(/\/api\/?$/, '');
  }

  resolveImageUrl(url: string | null | undefined): string | null {
    if (!url) return null;
    if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) {
      return url;
    }
    const cleanPath = url.startsWith('/') ? url : `/${url}`;
    return `${this.serverBaseUrl}${cleanPath}`;
  }

  private mapMachineryImage(img: MachineryImageResponse): MachineryImageResponse {
    return {
      ...img,
      imageUrl: this.resolveImageUrl(img.imageUrl) ?? img.imageUrl
    };
  }

  private mapMachinery(m: MachineryResponse): MachineryResponse {
    return {
      ...m,
      images: (m.images || []).map(img => this.mapMachineryImage(img))
    };
  }

  private mapRental(r: MachineryRentalResponse): MachineryRentalResponse {
    return {
      ...r,
      machineryPrimaryImageUrl: this.resolveImageUrl(r.machineryPrimaryImageUrl) ?? undefined
    };
  }

  // ─── Public Browse ───────────────────────────────────────────────────────

  getMachinery(filter: MachineryFilterRequest): Observable<PagedMachineryResponse> {
    let params = new HttpParams();

    if (filter.name) params = params.set('name', filter.name);
    if (filter.category) params = params.set('category', filter.category);
    if (filter.city) params = params.set('city', filter.city);
    if (filter.state) params = params.set('state', filter.state);
    if (filter.minRentPerDay != null) params = params.set('minRentPerDay', filter.minRentPerDay.toString());
    if (filter.maxRentPerDay != null) params = params.set('maxRentPerDay', filter.maxRentPerDay.toString());
    if (filter.isDriverIncluded != null) params = params.set('isDriverIncluded', filter.isDriverIncluded.toString());
    if (filter.page != null) params = params.set('page', filter.page.toString());
    if (filter.pageSize != null) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PagedMachineryResponse>(`${this.baseUrl}/machinery`, { params }).pipe(
      map(res => ({
        ...res,
        items: res.items.map(m => this.mapMachinery(m))
      }))
    );
  }

  getMachineryById(id: string): Observable<MachineryResponse> {
    return this.http.get<MachineryResponse>(`${this.baseUrl}/machinery/${id}`).pipe(
      map(m => this.mapMachinery(m))
    );
  }

  getAvailability(id: string): Observable<MachineryAvailabilityResponse> {
    return this.http.get<MachineryAvailabilityResponse>(`${this.baseUrl}/machinery/${id}/availability`);
  }

  // ─── Owner Management ─────────────────────────────────────────────────────

  getMyMachinery(): Observable<MachineryResponse[]> {
    return this.http.get<MachineryResponse[]>(`${this.baseUrl}/my-machinery`).pipe(
      map(list => list.map(m => this.mapMachinery(m)))
    );
  }

  createMachinery(request: CreateMachineryRequest): Observable<MachineryResponse> {
    return this.http.post<MachineryResponse>(`${this.baseUrl}/my-machinery`, request).pipe(
      map(m => this.mapMachinery(m))
    );
  }

  updateMachinery(id: string, request: UpdateMachineryRequest): Observable<MachineryResponse> {
    return this.http.put<MachineryResponse>(`${this.baseUrl}/my-machinery/${id}`, request).pipe(
      map(m => this.mapMachinery(m))
    );
  }

  deleteMachinery(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/my-machinery/${id}`);
  }

  uploadImage(id: string, file: File, isPrimary: boolean = false): Observable<MachineryImageResponse> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('isPrimary', isPrimary.toString());

    return this.http.post<MachineryImageResponse>(`${this.baseUrl}/my-machinery/${id}/images`, formData).pipe(
      map(img => this.mapMachineryImage(img))
    );
  }

  deleteImage(id: string, imageId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/my-machinery/${id}/images/${imageId}`);
  }

  setPrimaryImage(id: string, imageId: string): Observable<MachineryResponse> {
    return this.http.put<MachineryResponse>(`${this.baseUrl}/my-machinery/${id}/images/${imageId}/primary`, {}).pipe(
      map(m => this.mapMachinery(m))
    );
  }

  // ─── Rental Bookings ──────────────────────────────────────────────────────

  bookRental(machineryId: string, request: BookRentalRequest): Observable<MachineryRentalResponse> {
    return this.http.post<MachineryRentalResponse>(`${this.baseUrl}/machinery/${machineryId}/rentals`, request).pipe(
      map(r => this.mapRental(r))
    );
  }

  getMyRentals(): Observable<MachineryRentalResponse[]> {
    return this.http.get<MachineryRentalResponse[]>(`${this.baseUrl}/my-rentals`).pipe(
      map(list => list.map(r => this.mapRental(r)))
    );
  }

  getMyListingsRentals(): Observable<MachineryRentalResponse[]> {
    return this.http.get<MachineryRentalResponse[]>(`${this.baseUrl}/machinery-owner/rentals`).pipe(
      map(list => list.map(r => this.mapRental(r)))
    );
  }

  getRentalById(id: string): Observable<MachineryRentalResponse> {
    return this.http.get<MachineryRentalResponse>(`${this.baseUrl}/rentals/${id}`).pipe(
      map(r => this.mapRental(r))
    );
  }

  updateRentalStatus(id: string, request: UpdateRentalStatusRequest): Observable<MachineryRentalResponse> {
    return this.http.patch<MachineryRentalResponse>(`${this.baseUrl}/rentals/${id}/status`, request).pipe(
      map(r => this.mapRental(r))
    );
  }
}
