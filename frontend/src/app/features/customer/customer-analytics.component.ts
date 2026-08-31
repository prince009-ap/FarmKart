import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomerAnalyticsService } from '../../core/services/customer-analytics.service';
import { AnalyticsDateRangeRequest, CustomerAnalyticsOverview } from '../../core/models/analytics.models';
import { AnalyticsChartComponent } from '../../shared/analytics-chart.component';
import { AnalyticsDateFilterComponent } from '../../shared/analytics-date-filter.component';

@Component({
  selector: 'app-customer-analytics',
  standalone: true,
  imports: [
    TranslatePipe,
    CommonModule,
    MatButtonModule,
    MatIconModule,
    AnalyticsChartComponent,
    AnalyticsDateFilterComponent
  ],
  templateUrl: './customer-analytics.component.html'
})
export class CustomerAnalyticsComponent implements OnInit {
  private readonly analyticsService = inject(CustomerAnalyticsService);

  analyticsData = signal<CustomerAnalyticsOverview | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);
  currentFilter = signal<AnalyticsDateRangeRequest>({ range: 'Last30Days' as any });

  ngOnInit(): void {
    this.loadAnalytics();
  }

  onDateFilterChange(filter: AnalyticsDateRangeRequest): void {
    this.currentFilter.set(filter);
    this.loadAnalytics();
  }

  loadAnalytics(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.analyticsService.getCustomerAnalytics(this.currentFilter()).subscribe({
      next: (data) => {
        this.analyticsData.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load customer analytics:', err);
        this.errorMessage.set('Unable to load customer analytics data. Please check your network and try again.');
        this.isLoading.set(false);
      }
    });
  }
}
