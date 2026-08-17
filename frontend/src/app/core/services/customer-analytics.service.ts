import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AnalyticsDateRangeRequest, CustomerAnalyticsOverview } from '../models/analytics.models';

import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class CustomerAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/customer/analytics`;

  getCustomerAnalytics(request?: AnalyticsDateRangeRequest): Observable<CustomerAnalyticsOverview> {
    let params = new HttpParams();
    if (request?.range) {
      params = params.set('range', request.range);
    }
    if (request?.customStartDateUtc) {
      params = params.set('customStartDateUtc', request.customStartDateUtc);
    }
    if (request?.customEndDateUtc) {
      params = params.set('customEndDateUtc', request.customEndDateUtc);
    }

    return this.http.get<CustomerAnalyticsOverview>(this.baseUrl, { params });
  }
}
