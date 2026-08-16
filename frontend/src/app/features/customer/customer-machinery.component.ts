import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MachineryService } from '../../core/services/machinery.service';
import { PagedMachineryResponse, MACHINERY_CATEGORIES } from '../../core/models/machinery.models';
import { WishlistButtonComponent } from '../../shared/wishlist-button.component';

@Component({
  selector: 'app-customer-machinery',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    WishlistButtonComponent
  ],
  templateUrl: './customer-machinery.component.html'
})
export class CustomerMachineryComponent implements OnInit {
  private readonly machineryService = inject(MachineryService);
  private readonly router = inject(Router);

  result = signal<PagedMachineryResponse | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  categories = MACHINERY_CATEGORIES;

  // Filters State
  search = signal<string>('');
  selectedCategory = signal<string>('');
  brandSearch = signal<string>('');
  citySearch = signal<string>('');
  minPrice = signal<number | undefined>(undefined);
  maxPrice = signal<number | undefined>(undefined);
  driverAvailableFilter = signal<string>('all'); // 'all' | 'true' | 'false'
  startDate = signal<string>('');
  endDate = signal<string>('');
  sortBy = signal<string>('newest');
  currentPage = signal<number>(1);

  get newMachineryRoute(): string {
    return this.router.url.includes('/farmer/') ? '/farmer/machinery/new' : '/customer/my-machinery/new';
  }

  get myRentalsRoute(): string {
    return '/customer/my-rentals';
  }

  getDetailRoute(id: string): string {
    return this.router.url.includes('/farmer/') ? `/farmer/machinery/marketplace/${id}` : `/customer/machinery/${id}`;
  }

  getEditRoute(id: string): string {
    return this.router.url.includes('/farmer/') ? `/farmer/machinery/${id}/edit` : `/customer/my-machinery/${id}/edit`;
  }

  ngOnInit(): void {
    this.loadMachinery();
  }

  loadMachinery(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    let driverAvail: boolean | undefined = undefined;
    if (this.driverAvailableFilter() === 'true') driverAvail = true;
    if (this.driverAvailableFilter() === 'false') driverAvail = false;

    this.machineryService.getMachinery({
      search: this.search() || undefined,
      category: this.selectedCategory() || undefined,
      brand: this.brandSearch() || undefined,
      city: this.citySearch() || undefined,
      minRentPerDay: this.minPrice(),
      maxRentPerDay: this.maxPrice(),
      driverAvailable: driverAvail,
      startDate: this.startDate() || undefined,
      endDate: this.endDate() || undefined,
      sortBy: this.sortBy() || undefined,
      page: this.currentPage(),
      pageSize: 12
    }).subscribe({
      next: (res) => {
        this.result.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load machinery listings.');
        this.isLoading.set(false);
      }
    });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadMachinery();
  }

  resetFilters(): void {
    this.search.set('');
    this.selectedCategory.set('');
    this.brandSearch.set('');
    this.citySearch.set('');
    this.minPrice.set(undefined);
    this.maxPrice.set(undefined);
    this.driverAvailableFilter.set('all');
    this.startDate.set('');
    this.endDate.set('');
    this.sortBy.set('newest');
    this.currentPage.set(1);
    this.loadMachinery();
  }

  goToPage(page: number): void {
    this.currentPage.set(page);
    this.loadMachinery();
  }
}
