import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerOrderService } from './customer-order.service';
import { CustomerOrderDetail } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './customer-order-detail.component.html'
})
export class CustomerOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly orderService = inject(CustomerOrderService);

  order = signal<CustomerOrderDetail | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadOrderDetail(id);
    } else {
      this.errorMessage.set('Invalid order ID.');
      this.isLoading.set(false);
    }
  }

  loadOrderDetail(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.orderService.getCustomerOrderById(id).subscribe({
      next: (data) => {
        this.order.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        if (err?.status === 404 || err?.status === 403) {
          this.errorMessage.set('Order not found or you do not have permission to view this order.');
        } else {
          this.errorMessage.set('Unable to load order details.');
        }
        this.isLoading.set(false);
      }
    });
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/images/crop-placeholder.png';
  }
}
