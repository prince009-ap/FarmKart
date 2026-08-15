import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { InvoiceResponse } from '../models/invoice.models';

@Injectable({
  providedIn: 'root'
})
export class InvoiceService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  getCustomerInvoice(orderId: string): Observable<InvoiceResponse> {
    return this.http.get<InvoiceResponse>(`${this.apiUrl}/customer/orders/${orderId}/invoice`);
  }

  getFarmerInvoice(orderId: string): Observable<InvoiceResponse> {
    return this.http.get<InvoiceResponse>(`${this.apiUrl}/farmer/orders/${orderId}/invoice`);
  }
}
