import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AddCropStockRequest, CreateCropRequest, CropImage, CropStockSummary, CropStockTransaction, FarmerCrop, UpdateCropRequest } from '../../core/models/farmer-crop.models';

@Injectable({
  providedIn: 'root'
})
export class FarmerCropService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/farmer/crops`;

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

  private transformCrop(crop: FarmerCrop): FarmerCrop {
    return {
      ...crop,
      primaryImageUrl: this.resolveImageUrl(crop.primaryImageUrl),
      images: (crop.images || []).map(img => this.transformCropImage(img))
    };
  }

  private transformCropImage(img: CropImage): CropImage {
    return {
      ...img,
      imageUrl: this.resolveImageUrl(img.imageUrl) || img.imageUrl
    };
  }

  getCrops(): Observable<FarmerCrop[]> {
    return this.http.get<FarmerCrop[]>(this.apiUrl).pipe(
      map(crops => crops.map(crop => this.transformCrop(crop)))
    );
  }

  getCropById(id: string): Observable<FarmerCrop> {
    return this.http.get<FarmerCrop>(`${this.apiUrl}/${id}`).pipe(
      map(crop => this.transformCrop(crop))
    );
  }

  createCrop(request: CreateCropRequest): Observable<FarmerCrop> {
    return this.http.post<FarmerCrop>(this.apiUrl, request).pipe(
      map(crop => this.transformCrop(crop))
    );
  }

  updateCrop(id: string, request: UpdateCropRequest): Observable<FarmerCrop> {
    return this.http.put<FarmerCrop>(`${this.apiUrl}/${id}`, request).pipe(
      map(crop => this.transformCrop(crop))
    );
  }

  deleteCrop(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  uploadCropImage(cropId: string, file: File, isPrimary: boolean = false): Observable<CropImage> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('isPrimary', isPrimary.toString());
    return this.http.post<CropImage>(`${this.apiUrl}/${cropId}/images`, formData).pipe(
      map(img => this.transformCropImage(img))
    );
  }

  deleteCropImage(cropId: string, imageId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${cropId}/images/${imageId}`);
  }

  setPrimaryCropImage(cropId: string, imageId: string): Observable<FarmerCrop> {
    return this.http.put<FarmerCrop>(`${this.apiUrl}/${cropId}/images/${imageId}/primary`, {}).pipe(
      map(crop => this.transformCrop(crop))
    );
  }

  getCropStock(cropId: string): Observable<CropStockSummary> {
    return this.http.get<CropStockSummary>(`${this.apiUrl}/${cropId}/stock`);
  }

  addCropStock(cropId: string, request: AddCropStockRequest): Observable<CropStockSummary> {
    return this.http.post<CropStockSummary>(`${this.apiUrl}/${cropId}/stock`, request);
  }

  getCropStockHistory(cropId: string): Observable<CropStockTransaction[]> {
    return this.http.get<CropStockTransaction[]>(`${this.apiUrl}/${cropId}/stock/history`);
  }
}
