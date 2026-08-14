import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { WorkerEarningsComponent } from './worker-earnings.component';
import { WorkerJobService } from './worker-job.service';
import { WorkerEarningsSummary } from '../../core/models/worker.models';

describe('WorkerEarningsComponent', () => {
  let component: WorkerEarningsComponent;
  let fixture: ComponentFixture<WorkerEarningsComponent>;
  let mockWorkerService: any;

  const mockEarningsSummary: WorkerEarningsSummary = {
    totalEarnings: 3600,
    completedJobsCount: 2,
    thisMonthEarnings: 3600,
    allTimeEarnings: 3600,
    earningsHistory: [
      {
        assignmentId: 'assign-1',
        jobId: 'job-1',
        jobTitle: 'Wheat Harvesting',
        farmerName: 'Ramesh Patel',
        workCategory: 'Harvesting',
        startDate: '2026-08-01',
        endDate: '2026-08-06',
        daysWorked: 6,
        dailyWage: 600,
        totalEarned: 3600,
        status: 'Completed',
        assignedAtUtc: '2026-08-01T00:00:00Z'
      }
    ]
  };

  beforeEach(async () => {
    mockWorkerService = {
      getEarnings: vi.fn().mockReturnValue(of(mockEarningsSummary))
    };

    await TestBed.configureTestingModule({
      imports: [WorkerEarningsComponent, HttpClientTestingModule],
      providers: [
        { provide: WorkerJobService, useValue: mockWorkerService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerEarningsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should load worker earnings summary on init', () => {
    fixture.detectChanges();
    expect(mockWorkerService.getEarnings).toHaveBeenCalled();
    expect(component.loading()).toBe(false);
    expect(component.summary()).toEqual(mockEarningsSummary);
  });

  it('should display total earnings banner correctly', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('₹3,600');
  });

  it('should display completed jobs count correctly', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Completed Jobs');
    expect(compiled.textContent).toContain('2');
  });

  it('should render earnings history table with item details', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Wheat Harvesting');
    expect(compiled.textContent).toContain('Ramesh Patel');
    expect(compiled.textContent).toContain('6 days');
  });

  it('should display empty state when earnings history is empty', () => {
    const emptySummary: WorkerEarningsSummary = {
      totalEarnings: 0,
      completedJobsCount: 0,
      thisMonthEarnings: 0,
      allTimeEarnings: 0,
      earningsHistory: []
    };
    mockWorkerService.getEarnings.mockReturnValue(of(emptySummary));

    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No Earnings History Yet');
  });

  it('should handle error when loading earnings fails', () => {
    mockWorkerService.getEarnings.mockReturnValue(throwError(() => new Error('Server error')));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.loadError()).toContain('Failed to load earnings history');
  });

  it('should retry loading earnings on button click', () => {
    mockWorkerService.getEarnings.mockReturnValue(throwError(() => new Error('Server error')));
    fixture.detectChanges();

    expect(component.loadError()).toBeTruthy();

    mockWorkerService.getEarnings.mockReturnValue(of(mockEarningsSummary));
    component.loadEarnings();

    expect(component.summary()).toEqual(mockEarningsSummary);
    expect(component.loadError()).toBeNull();
  });
});
