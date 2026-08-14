import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkerWorkHistoryComponent } from './worker-work-history.component';
import { WorkerJobService } from './worker-job.service';
import { of, throwError } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { WorkerWorkHistorySummary } from '../../core/models/worker.models';

describe('WorkerWorkHistoryComponent', () => {
  let component: WorkerWorkHistoryComponent;
  let fixture: ComponentFixture<WorkerWorkHistoryComponent>;
  let mockWorkerService: any;

  const dummySummary: WorkerWorkHistorySummary = {
    totalCompletedJobs: 1,
    totalWorkDays: 5,
    totalEarnings: 3000,
    historyItems: [
      {
        assignmentId: 'assign-1',
        jobId: 'job-1',
        jobTitle: 'Wheat Harvesting Worker',
        workCategory: 'Harvesting',
        farmerName: 'Prince Patel',
        location: 'Kerali Farm',
        startDate: '2026-08-20',
        endDate: '2026-08-25',
        dailyWage: 600,
        daysWorked: 5,
        presentCount: 5,
        halfDayCount: 0,
        totalEarned: 3000,
        rating: 5,
        reviewComment: 'Great work',
        status: 'Completed',
        completedAtUtc: '2026-08-25T10:00:00Z'
      }
    ]
  };

  beforeEach(async () => {
    mockWorkerService = {
      getWorkHistory: vi.fn().mockReturnValue(of(dummySummary))
    };

    await TestBed.configureTestingModule({
      imports: [WorkerWorkHistoryComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: WorkerJobService, useValue: mockWorkerService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerWorkHistoryComponent);
    component = fixture.componentInstance;
  });

  it('1. Work history page loads', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(mockWorkerService.getWorkHistory).toHaveBeenCalled();
  });

  it('2. Completed jobs display', () => {
    fixture.detectChanges();
    expect(component.summary()?.totalCompletedJobs).toBe(1);
    expect(component.filteredItems().length).toBe(1);
  });

  it('3. Job information displays', () => {
    fixture.detectChanges();
    const item = component.filteredItems()[0];
    expect(item.jobTitle).toBe('Wheat Harvesting Worker');
    expect(item.farmerName).toBe('Prince Patel');
    expect(item.location).toBe('Kerali Farm');
  });

  it('4. Attendance summary displays', () => {
    fixture.detectChanges();
    const item = component.filteredItems()[0];
    expect(item.presentCount).toBe(5);
    expect(item.halfDayCount).toBe(0);
  });

  it('5. Earnings display correctly', () => {
    fixture.detectChanges();
    expect(component.summary()?.totalEarnings).toBe(3000);
    expect(component.filteredItems()[0].totalEarned).toBe(3000);
  });

  it('6. Rating displays when available', () => {
    fixture.detectChanges();
    expect(component.filteredItems()[0].rating).toBe(5);
    expect(component.filteredItems()[0].reviewComment).toBe('Great work');
  });

  it('7. Empty state works', () => {
    mockWorkerService.getWorkHistory.mockReturnValue(of({
      totalCompletedJobs: 0,
      totalWorkDays: 0,
      totalEarnings: 0,
      historyItems: []
    }));

    component.loadHistory();
    fixture.detectChanges();

    expect(component.filteredItems().length).toBe(0);
    expect(component.summary()?.totalCompletedJobs).toBe(0);
  });

  it('8. Loading state works', () => {
    expect(component.loading()).toBe(true);
  });

  it('9. API error state works', () => {
    mockWorkerService.getWorkHistory.mockReturnValue(throwError(() => new Error('API Error')));
    component.loadHistory();
    fixture.detectChanges();

    expect(component.loadError()).toBeTruthy();
    expect(component.loading()).toBe(false);
  });

  it('10. Filters work', () => {
    fixture.detectChanges();

    // Filter Rated
    component.onFilterChange('Rated');
    expect(component.filteredItems().length).toBe(1);

    // Filter Unrated
    component.onFilterChange('Unrated');
    expect(component.filteredItems().length).toBe(0);
  });
});
