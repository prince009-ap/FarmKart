import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
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

  result = signal<PagedMachineryResponse | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  categories = MACHINERY_CATEGORIES;

  // Filters
  nameSearch = signal<string>('');
  selectedCategory = signal<string>('');
  citySearch = signal<string>('');
  minPrice = signal<number | undefined>(undefined);
  maxPrice = signal<number | undefined>(undefined);
  driverFilter = signal<boolean | undefined>(undefined);
  currentPage = signal<number>(1);

  ngOnInit(): void {
    this.loadMachinery();
  }

  loadMachinery(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.machineryService.getMachinery({
      name: this.nameSearch() || undefined,
      category: this.selectedCategory() || undefined,
      city: this.citySearch() || undefined,
      minRentPerDay: this.minPrice(),
      maxRentPerDay: this.maxPrice(),
      isDriverIncluded: this.driverFilter(),
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
    this.nameSearch.set('');
    this.selectedCategory.set('');
    this.citySearch.set('');
    this.minPrice.set(undefined);
    this.maxPrice.set(undefined);
    this.driverFilter.set(undefined);
    this.currentPage.set(1);
    this.loadMachinery();
  }

  goToPage(page: number): void {
    this.currentPage.set(page);
    this.loadMachinery();
  }
}
