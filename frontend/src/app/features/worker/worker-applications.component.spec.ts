import { TestBed, ComponentFixture } from '@angular/core/testing';
import { WorkerApplicationsComponent } from './worker-applications.component';
import { WorkerJobService } from './worker-job.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkerJobApplication } from '../../core/models/worker.models';

describe('WorkerApplicationsComponent', () => {
  let component: WorkerApplicationsComponent;
  let fixture: ComponentFixture<WorkerApplicationsComponent>;
  let jobServiceMock: any;

  const mockApplications: WorkerJobApplication[] = [
    {
      applicationId: 'app-1',
      jobId: 'wjob-1',
      jobTitle: 'Rice Harvesting Worker',
      workCategory: 'Harvesting',
      wagePerDay: 500,
      startDate: '2026-08-20',
      endDate: '2026-08-25',
      farmLocation: 'Nadiad Farm',
      status: 'Pending',
      appliedAtUtc: '2026-08-13T12:00:00Z',
      message: 'Available all days.'
    }
  ];

  beforeEach(async () => {
    jobServiceMock = {
      getAvailableJobs: vi.fn(),
      getJobDetails: vi.fn(),
      applyToJob: vi.fn(),
      getMyApplications: vi.fn().mockReturnValue(of([]))
    };

    await TestBed.configureTestingModule({
      imports: [WorkerApplicationsComponent, NoopAnimationsModule],
      providers: [
        { provide: WorkerJobService, useValue: jobServiceMock },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerApplicationsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load and display worker submitted applications', () => {
    jobServiceMock.getMyApplications.mockReturnValue(of(mockApplications));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.applications()).toEqual(mockApplications);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Rice Harvesting Worker');
    expect(compiled.textContent).toContain('Pending');
    expect(compiled.textContent).toContain('₹500');
  });

  it('should render empty state when worker has no applications', () => {
    jobServiceMock.getMyApplications.mockReturnValue(of([]));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.applications()).toEqual([]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('No applications submitted yet');
  });

  it('should handle API errors safely', () => {
    jobServiceMock.getMyApplications.mockReturnValue(throwError(() => ({ status: 500 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Unable to load your applications. Please try again.');
  });
});
