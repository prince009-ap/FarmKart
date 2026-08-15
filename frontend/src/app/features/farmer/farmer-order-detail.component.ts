import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FarmerOrderService } from './farmer-order.service';
import { FarmerOrderDetail } from '../../core/models/farmer-order.models';

@Component({
  selector: 'app-farmer-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './farmer-order-detail.component.html'
})
export class FarmerOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly farmerOrderService = inject(FarmerOrderService);

  order = signal<FarmerOrderDetail | null>(null);
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

    this.farmerOrderService.getFarmerOrderById(id).subscribe({
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

  onImageError(): void {
    const current = this.order();
    if (current) {
      this.order.set({ ...current, primaryImageUrl: null });
    }
  }
}
