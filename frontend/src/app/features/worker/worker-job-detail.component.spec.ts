import { TestBed, ComponentFixture } from '@angular/core/testing';
import { WorkerJobDetailComponent } from './worker-job-detail.component';
import { WorkerJobService } from './worker-job.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkerAvailableJob } from '../../core/models/worker.models';

describe('WorkerJobDetailComponent', () => {
  let component: WorkerJobDetailComponent;
  let fixture: ComponentFixture<WorkerJobDetailComponent>;
  let jobServiceMock: any;

  const mockDetailJob: WorkerAvailableJob = {
    id: 'wjob-100',
    title: 'Wheat Harvesting Special',
    description: 'Specialized harvesting labor needed in Valley',
    workCategory: 'Harvesting',
    cropType: 'Wheat',
    workersRequired: 5,
    requiredExperience: 2,
    wagePerDay: 600,
    startDate: '2026-08-20',
    endDate: '2026-08-25',
    workingHours: '8 AM - 5 PM',
    farmLocation: 'Valley Farm',
    farmSize: 10,
    foodProvided: true,
    accommodationProvided: true,
    isUrgent: true,
    status: 'Open',
    createdAtUtc: '2026-08-13T10:00:00Z',
    hasApplied: false,
    farmerName: 'Farmer Ramesh'
  };

  beforeEach(async () => {
    jobServiceMock = {
      getAvailableJobs: vi.fn(),
      getJobDetails: vi.fn().mockReturnValue(of(mockDetailJob)),
      applyToJob: vi.fn(),
      getMyApplications: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [WorkerJobDetailComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: WorkerJobService, useValue: jobServiceMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'wjob-100' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerJobDetailComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load and display job details', () => {
    fixture.detectChanges();

    expect(jobServiceMock.getJobDetails).toHaveBeenCalledWith('wjob-100');
    expect(component.loading()).toBe(false);
    expect(component.job()).toEqual(mockDetailJob);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Wheat Harvesting Special');
    expect(compiled.textContent).toContain('₹600');
  });

  it('should call applyToJob API on Submit Application click and update UI', () => {
    fixture.detectChanges();

    jobServiceMock.applyToJob.mockReturnValue(of({ applicationId: 'app-1', status: 'Pending' }));
    component.applicationMessage.set('Experienced in wheat harvest');
    component.applyNow();
    fixture.detectChanges();

    expect(jobServiceMock.applyToJob).toHaveBeenCalledWith('wjob-100', { message: 'Experienced in wheat harvest' });
    expect(component.submitSuccess()).toBe(true);
    expect(component.job()?.hasApplied).toBe(true);
  });

  it('should handle 409 Conflict duplicate application error and set already applied state', () => {
    fixture.detectChanges();

    jobServiceMock.applyToJob.mockReturnValue(throwError(() => ({ status: 409, error: { message: 'You have already applied to this job.' } })));
    component.applyNow();
    fixture.detectChanges();

    expect(component.submitError()).toBe('You have already applied to this job.');
    expect(component.job()?.hasApplied).toBe(true);
  });
});
