import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FarmerAnalyticsService } from '../../core/services/farmer-analytics.service';
import { AnalyticsDateRangeRequest, FarmerAnalyticsOverview } from '../../core/models/analytics.models';
import { AnalyticsChartComponent } from '../../shared/analytics-chart.component';
import { AnalyticsDateFilterComponent } from '../../shared/analytics-date-filter.component';

@Component({
  selector: 'app-farmer-analytics',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    AnalyticsChartComponent,
    AnalyticsDateFilterComponent
  ],
  templateUrl: './farmer-analytics.component.html'
})
export class FarmerAnalyticsComponent implements OnInit {
  private readonly analyticsService = inject(FarmerAnalyticsService);

  analyticsData = signal<FarmerAnalyticsOverview | null>(null);
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

    this.analyticsService.getFarmerAnalytics(this.currentFilter()).subscribe({
      next: (data) => {
        this.analyticsData.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load farmer analytics:', err);
        this.errorMessage.set('Unable to load analytics data. Please check your network and try again.');
        this.isLoading.set(false);
      }
    });
  }
}
