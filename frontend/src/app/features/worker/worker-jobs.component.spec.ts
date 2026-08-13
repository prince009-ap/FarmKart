import { TestBed, ComponentFixture } from '@angular/core/testing';
import { WorkerJobsComponent } from './worker-jobs.component';
import { WorkerJobService } from './worker-job.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkerAvailableJob } from '../../core/models/worker.models';

describe('WorkerJobsComponent', () => {
  let component: WorkerJobsComponent;
  let fixture: ComponentFixture<WorkerJobsComponent>;
  let jobServiceMock: any;

  const mockAvailableJobs: WorkerAvailableJob[] = [
    {
      id: 'wjob-1',
      title: 'Rice Harvesting Worker',
      description: 'Need workers for rice harvesting in Nadiad',
      workCategory: 'Harvesting',
      cropType: 'Rice',
      workersRequired: 4,
      requiredExperience: 1,
      wagePerDay: 500,
      startDate: '2026-08-20',
      endDate: '2026-08-25',
      workingHours: '8 AM - 5 PM',
      farmLocation: 'Nadiad Farm',
      farmSize: 12,
      foodProvided: true,
      accommodationProvided: false,
      isUrgent: true,
      status: 'Open',
      createdAtUtc: '2026-08-13T10:00:00Z',
      hasApplied: false,
      farmerName: 'Ramesh Patel'
    },
    {
      id: 'wjob-2',
      title: 'Cotton Weeding Labor',
      description: 'Weeding cotton field in Anand',
      workCategory: 'Weeding',
      cropType: 'Cotton',
      workersRequired: 2,
      requiredExperience: 0,
      wagePerDay: 450,
      startDate: '2026-08-22',
      endDate: '2026-08-24',
      workingHours: '7 AM - 4 PM',
      farmLocation: 'Anand Farm',
      farmSize: 5,
      foodProvided: false,
      accommodationProvided: false,
      isUrgent: false,
      status: 'Open',
      createdAtUtc: '2026-08-12T10:00:00Z',
      hasApplied: true,
      farmerName: 'Suresh Kumar'
    }
  ];

  beforeEach(async () => {
    jobServiceMock = {
      getAvailableJobs: vi.fn().mockReturnValue(of([])),
      getJobDetails: vi.fn(),
      applyToJob: vi.fn(),
      getMyApplications: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [WorkerJobsComponent, NoopAnimationsModule],
      providers: [
        { provide: WorkerJobService, useValue: jobServiceMock },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerJobsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load and display available jobs', () => {
    jobServiceMock.getAvailableJobs.mockReturnValue(of(mockAvailableJobs));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.jobs()).toEqual(mockAvailableJobs);
    expect(component.filteredJobs().length).toBe(2);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Rice Harvesting Worker');
    expect(compiled.textContent).toContain('Cotton Weeding Labor');
  });

  it('should render empty state "No jobs available right now." when API returns empty list', () => {
    jobServiceMock.getAvailableJobs.mockReturnValue(of([]));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.jobs()).toEqual([]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('No jobs available right now.');
  });

  it('should filter jobs by title/crop search term', () => {
    jobServiceMock.getAvailableJobs.mockReturnValue(of(mockAvailableJobs));
    fixture.detectChanges();

    component.searchTerm.set('Cotton');
    fixture.detectChanges();

    expect(component.filteredJobs().length).toBe(1);
    expect(component.filteredJobs()[0].title).toBe('Cotton Weeding Labor');
  });

  it('should handle API errors safely and display retry button', () => {
    jobServiceMock.getAvailableJobs.mockReturnValue(throwError(() => ({ status: 500 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Unable to load available jobs. Please try again.');

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Unable to load available jobs.');
  });
});
