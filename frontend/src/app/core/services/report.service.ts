import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateReportRequest, UserReportResponse, ReportQueryRequest, PagedReportResponse } from '../models/report.models';

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/reports`;

  createReport(request: CreateReportRequest): Observable<UserReportResponse> {
    return this.http.post<UserReportResponse>(this.apiUrl, request);
  }

  getUserReports(params?: ReportQueryRequest): Observable<PagedReportResponse> {
    let httpParams = new HttpParams();
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.targetType) httpParams = httpParams.set('targetType', params.targetType);
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PagedReportResponse>(this.apiUrl, { params: httpParams });
  }

  getReportById(id: string): Observable<UserReportResponse> {
    return this.http.get<UserReportResponse>(`${this.apiUrl}/${id}`);
  }
}
