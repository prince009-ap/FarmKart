import { TestBed, ComponentFixture } from '@angular/core/testing';
import { FarmerJobsComponent } from './farmer-jobs.component';
import { FarmerJobService } from './farmer-job.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { FarmerJob } from '../../core/models/farmer.models';

describe('FarmerJobsComponent', () => {
  let component: FarmerJobsComponent;
  let fixture: ComponentFixture<FarmerJobsComponent>;
  let jobServiceMock: any;

  const mockJobs: FarmerJob[] = [
    {
      id: 'job-1',
      title: 'Harvesting Help Required',
      description: 'Need experienced workers for wheat harvest',
      workCategory: 'Harvesting',
      cropType: 'Wheat',
      workersRequired: 3,
      requiredExperience: 1,
      wagePerDay: 550,
      startDate: '2026-08-20',
      endDate: '2026-08-25',
      workingHours: '8 AM - 5 PM',
      farmLocation: 'Valley Farm',
      farmSize: 10,
      foodProvided: true,
      accommodationProvided: false,
      isUrgent: true,
      status: 'Open',
      createdAtUtc: '2026-08-13T10:00:00Z'
    }
  ];

  beforeEach(async () => {
    jobServiceMock = {
      getMyJobs: vi.fn().mockReturnValue(of([])),
      deleteJob: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [FarmerJobsComponent, NoopAnimationsModule],
      providers: [
        { provide: FarmerJobService, useValue: jobServiceMock },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerJobsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should render empty state when API returns HTTP 200 with empty job list []', () => {
    jobServiceMock.getMyJobs.mockReturnValue(of([]));
    fixture.detectChanges();

    expect(jobServiceMock.getMyJobs).toHaveBeenCalledTimes(1);
    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('');
    expect(component.jobs()).toEqual([]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('No jobs posted yet');
    expect(compiled.textContent).not.toContain('Unable to load your jobs.');
  });

  it('should render job list when API returns jobs successfully', () => {
    jobServiceMock.getMyJobs.mockReturnValue(of(mockJobs));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('');
    expect(component.jobs()).toEqual(mockJobs);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Harvesting Help Required');
    expect(compiled.textContent).not.toContain('No jobs posted yet.');
  });

  it('should handle 401, 403, or 500 API errors and display error message', () => {
    jobServiceMock.getMyJobs.mockReturnValue(throwError(() => ({ status: 500 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Unable to load your jobs. Please try again.');
    expect(component.jobs()).toEqual([]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Unable to load your jobs. Please try again.');
  });

  it('should reload jobs when retry is triggered', () => {
    jobServiceMock.getMyJobs.mockReturnValueOnce(throwError(() => ({ status: 500 })));
    fixture.detectChanges();

    expect(component.error()).toBe('Unable to load your jobs. Please try again.');

    jobServiceMock.getMyJobs.mockReturnValueOnce(of(mockJobs));
    component.loadJobs();
    fixture.detectChanges();

    expect(component.error()).toBe('');
    expect(component.jobs()).toEqual(mockJobs);
  });

  it('should set loading state correctly while request is pending', () => {
    expect(component.loading()).toBe(true);
    fixture.detectChanges();
    expect(component.loading()).toBe(false);
  });
});
