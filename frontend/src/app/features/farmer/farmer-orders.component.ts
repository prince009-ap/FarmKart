import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { FarmerOrderService } from './farmer-order.service';
import {
  FarmerOrderListItem,
  FarmerOrderSummary
} from '../../core/models/farmer-order.models';

@Component({
  selector: 'app-farmer-orders',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule
  ],
  templateUrl: './farmer-orders.component.html'
})
export class FarmerOrdersComponent implements OnInit {
  private readonly farmerOrderService = inject(FarmerOrderService);

  summary = signal<FarmerOrderSummary | null>(null);
  orders = signal<FarmerOrderListItem[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  searchQuery = signal<string>('');
  selectedStatus = signal<string>('ALL');

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.farmerOrderService.getFarmerOrderSummary().subscribe({
      next: (summaryData) => this.summary.set(summaryData),
      error: () => {}
    });

    const filter = {
      search: this.searchQuery(),
      status: this.selectedStatus() === 'ALL' ? '' : this.selectedStatus()
    };

    this.farmerOrderService.getFarmerOrders(filter).subscribe({
      next: (data) => {
        this.orders.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Unable to load your orders.');
        this.isLoading.set(false);
      }
    });
  }

  onSearchChange(): void {
    this.loadOrders();
  }

  onStatusChange(status: string): void {
    this.selectedStatus.set(status);
    this.loadOrders();
  }

  onImageError(item: FarmerOrderListItem): void {
    item.primaryImageUrl = null;
  }
}
