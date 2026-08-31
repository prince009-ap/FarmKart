import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { CustomerOrderService } from './customer-order.service';
import { CustomerOrderFilter, CustomerOrderListItem } from '../../core/models/customer-auction.models';

@Component({
  selector: 'app-customer-orders',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule
  ],
  templateUrl: './customer-orders.component.html'
})
export class CustomerOrdersComponent implements OnInit {
  private readonly orderService = inject(CustomerOrderService);

  orders = signal<CustomerOrderListItem[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  searchQuery = signal<string>('');
  selectedStatus = signal<string>('ALL');
  selectedSortBy = signal<string>('newest');

  readonly statusOptions = [
    { value: 'ALL', label: 'All Orders' },
    { value: 'ACTIVE', label: 'Active Orders' },
    { value: 'COMPLETED', label: 'Completed Orders' },
    { value: 'CONFIRMED', label: 'Confirmed' },
    { value: 'READY_FOR_PICKUP', label: 'Ready for Pickup' },
    { value: 'DISPATCHED', label: 'Dispatched' },
    { value: 'DELIVERED', label: 'Delivered' },
    { value: 'CANCELLED', label: 'Cancelled' }
  ];

  readonly sortOptions = [
    { value: 'newest', label: 'Newest First' },
    { value: 'oldest', label: 'Oldest First' }
  ];

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const filter: CustomerOrderFilter = {
      search: this.searchQuery().trim() || undefined,
      status: this.selectedStatus() !== 'ALL' ? this.selectedStatus() : undefined,
      sortBy: this.selectedSortBy()
    };

    this.orderService.getCustomerOrders(filter).subscribe({
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

  onSearchChange(val: string): void {
    this.searchQuery.set(val);
    this.loadOrders();
  }

  onStatusChange(val: string): void {
    this.selectedStatus.set(val);
    this.loadOrders();
  }

  onSortChange(val: string): void {
    this.selectedSortBy.set(val);
    this.loadOrders();
  }

  onImageError(item: CustomerOrderListItem): void {
    item.primaryImageUrl = null;
  }
}
