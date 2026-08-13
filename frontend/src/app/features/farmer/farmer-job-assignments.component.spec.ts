import { TestBed, ComponentFixture } from '@angular/core/testing';
import { FarmerJobAssignmentsComponent } from './farmer-job-assignments.component';
import { FarmerJobService } from './farmer-job.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { FarmerJob, FarmerWorkerAssignment } from '../../core/models/farmer.models';

describe('FarmerJobAssignmentsComponent', () => {
  let component: FarmerJobAssignmentsComponent;
  let fixture: ComponentFixture<FarmerJobAssignmentsComponent>;
  let jobServiceMock: any;

  const mockJob: FarmerJob = {
    id: 'fjob-200',
    title: 'Plowing Paddy Field',
    description: 'Plowing work',
    workCategory: 'Plowing',
    cropType: 'Paddy',
    workersRequired: 2,
    requiredExperience: 1,
    wagePerDay: 700,
    startDate: '2026-08-20',
    endDate: '2026-08-25',
    workingHours: '8 AM - 5 PM',
    farmLocation: 'Surat Farm',
    farmSize: 5,
    foodProvided: true,
    accommodationProvided: false,
    isUrgent: false,
    status: 'Open',
    createdAtUtc: '2026-08-14T00:00:00Z'
  };

  const mockAssignments: FarmerWorkerAssignment[] = [
    {
      assignmentId: 'assign-1',
      jobId: 'fjob-200',
      jobTitle: 'Plowing Paddy Field',
      workerProfileId: 'wprof-1',
      workerName: 'Suresh Fieldworker',
      workerPhone: '9876543210',
      workerExperienceYears: 4,
      workerSkills: ['Plowing', 'Tractor Operator'],
      startDate: '2026-08-20',
      endDate: '2026-08-25',
      assignedAtUtc: '2026-08-14T01:00:00Z',
      status: 'Active'
    }
  ];

  beforeEach(async () => {
    jobServiceMock = {
      getJob: vi.fn().mockReturnValue(of(mockJob)),
      getJobAssignments: vi.fn().mockReturnValue(of(mockAssignments))
    };

    await TestBed.configureTestingModule({
      imports: [FarmerJobAssignmentsComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: FarmerJobService, useValue: jobServiceMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ jobId: 'fjob-200' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerJobAssignmentsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load and display assigned workers for job', () => {
    fixture.detectChanges();

    expect(jobServiceMock.getJob).toHaveBeenCalledWith('fjob-200');
    expect(jobServiceMock.getJobAssignments).toHaveBeenCalledWith('fjob-200');
    expect(component.loading()).toBe(false);
    expect(component.assignments()).toEqual(mockAssignments);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Plowing Paddy Field');
    expect(compiled.textContent).toContain('Suresh Fieldworker');
    expect(compiled.textContent).toContain('Active');
  });

  it('should display empty state when zero workers assigned', () => {
    jobServiceMock.getJobAssignments.mockReturnValue(of([]));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.assignments()).toEqual([]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('No workers assigned to this job yet.');
  });

  it('should handle errors when loading assignments fails', () => {
    jobServiceMock.getJob.mockReturnValue(throwError(() => ({ status: 404 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Job not found.');
  });
});
