import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { InvoiceService } from '../../core/services/invoice.service';
import { InvoiceResponse } from '../../core/models/invoice.models';

@Component({
  selector: 'app-customer-invoice',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './customer-invoice.component.html'
})
export class CustomerInvoiceComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly invoiceService = inject(InvoiceService);

  invoice = signal<InvoiceResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('id');
    if (!orderId) {
      this.error.set('Order ID not specified.');
      this.loading.set(false);
      return;
    }
    this.loadInvoice(orderId);
  }

  loadInvoice(orderId: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.invoiceService.getCustomerInvoice(orderId).subscribe({
      next: (data) => {
        this.invoice.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 400 && err.error?.message) {
          this.error.set(err.error.message);
        } else {
          this.error.set('Unable to generate invoice. Please ensure the order payment is completed.');
        }
      }
    });
  }

  printInvoice(): void {
    window.print();
  }
}
