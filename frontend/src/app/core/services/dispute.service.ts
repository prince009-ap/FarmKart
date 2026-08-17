import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateDisputeRequest, UserDisputeResponse, DisputeQueryRequest, PagedDisputeResponse } from '../models/dispute.models';

@Injectable({
  providedIn: 'root'
})
export class DisputeService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/disputes`;

  createDispute(request: CreateDisputeRequest): Observable<UserDisputeResponse> {
    return this.http.post<UserDisputeResponse>(this.apiUrl, request);
  }

  getUserDisputes(params?: DisputeQueryRequest): Observable<PagedDisputeResponse> {
    let httpParams = new HttpParams();
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.relatedEntityType) httpParams = httpParams.set('relatedEntityType', params.relatedEntityType);
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PagedDisputeResponse>(this.apiUrl, { params: httpParams });
  }

  getDisputeById(id: string): Observable<UserDisputeResponse> {
    return this.http.get<UserDisputeResponse>(`${this.apiUrl}/${id}`);
  }

  closeDispute(id: string, resolutionNote?: string): Observable<UserDisputeResponse> {
    return this.http.post<UserDisputeResponse>(`${this.apiUrl}/${id}/close`, { resolutionNote });
  }
}
