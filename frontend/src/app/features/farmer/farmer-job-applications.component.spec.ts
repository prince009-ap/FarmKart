import { TestBed, ComponentFixture } from '@angular/core/testing';
import { FarmerJobApplicationsComponent } from './farmer-job-applications.component';
import { FarmerJobService } from './farmer-job.service';
import { ConfirmDialogService } from '../../shared/dialogs/confirm-dialog.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { FarmerJob, FarmerJobApplication } from '../../core/models/farmer.models';

describe('FarmerJobApplicationsComponent', () => {
  let component: FarmerJobApplicationsComponent;
  let fixture: ComponentFixture<FarmerJobApplicationsComponent>;
  let jobServiceMock: any;
  let confirmDialogMock: any;

  const mockJob: FarmerJob = {
    id: 'fjob-100',
    title: 'Wheat Harvesting Job',
    description: 'Wheat harvesting work',
    workCategory: 'Harvesting',
    cropType: 'Wheat',
    workersRequired: 2,
    requiredExperience: 1,
    wagePerDay: 600,
    startDate: '2026-08-20',
    endDate: '2026-08-25',
    workingHours: '8 AM - 5 PM',
    farmLocation: 'Nadiad Farm',
    farmSize: 10,
    foodProvided: true,
    accommodationProvided: false,
    isUrgent: true,
    status: 'Open',
    createdAtUtc: '2026-08-13T10:00:00Z'
  };

  const mockApplications: FarmerJobApplication[] = [
    {
      applicationId: 'app-1',
      jobId: 'fjob-100',
      jobTitle: 'Wheat Harvesting Job',
      applicantWorkerId: 'worker-1',
      applicantName: 'Ramesh Labor',
      applicantPhone: '9876543210',
      applicantExperienceYears: 3,
      applicantSkills: ['Harvesting', 'Tractor Driving'],
      status: 'Pending',
      appliedAtUtc: '2026-08-13T12:00:00Z',
      message: 'Experienced in wheat harvesting'
    }
  ];

  beforeEach(async () => {
    jobServiceMock = {
      getJob: vi.fn().mockReturnValue(of(mockJob)),
      getJobApplications: vi.fn().mockReturnValue(of(mockApplications)),
      acceptApplication: vi.fn(),
      rejectApplication: vi.fn()
    };

    confirmDialogMock = {
      confirm: vi.fn().mockReturnValue(of(true))
    };

    await TestBed.configureTestingModule({
      imports: [FarmerJobApplicationsComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: FarmerJobService, useValue: jobServiceMock },
        { provide: ConfirmDialogService, useValue: confirmDialogMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ jobId: 'fjob-100' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerJobApplicationsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load and display applications for farmer job', () => {
    fixture.detectChanges();

    expect(jobServiceMock.getJob).toHaveBeenCalledWith('fjob-100');
    expect(jobServiceMock.getJobApplications).toHaveBeenCalledWith('fjob-100');
    expect(component.loading()).toBe(false);
    expect(component.applications()).toEqual(mockApplications);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Wheat Harvesting Job');
    expect(compiled.textContent).toContain('Ramesh Labor');
    expect(compiled.textContent).toContain('Pending');
  });

  it('should accept pending application when dialog is confirmed', () => {
    const updatedApp: FarmerJobApplication = { ...mockApplications[0], status: 'Accepted' };
    jobServiceMock.acceptApplication.mockReturnValue(of(updatedApp));

    fixture.detectChanges();
    component.acceptApplication(mockApplications[0]);
    fixture.detectChanges();

    expect(confirmDialogMock.confirm).toHaveBeenCalled();
    expect(jobServiceMock.acceptApplication).toHaveBeenCalledWith('app-1');
    expect(component.applications()[0].status).toBe('Accepted');
    expect(component.actionMessage()).toContain('Successfully accepted Ramesh Labor');
  });

  it('should reject pending application when dialog is confirmed', () => {
    const updatedApp: FarmerJobApplication = { ...mockApplications[0], status: 'Rejected' };
    jobServiceMock.rejectApplication.mockReturnValue(of(updatedApp));

    fixture.detectChanges();
    component.rejectApplication(mockApplications[0]);
    fixture.detectChanges();

    expect(confirmDialogMock.confirm).toHaveBeenCalled();
    expect(jobServiceMock.rejectApplication).toHaveBeenCalledWith('app-1');
    expect(component.applications()[0].status).toBe('Rejected');
    expect(component.actionMessage()).toContain('Rejected Ramesh Labor');
  });

  it('should display empty state when job has zero applications', () => {
    jobServiceMock.getJobApplications.mockReturnValue(of([]));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.applications()).toEqual([]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('No applications yet');
  });

  it('should handle API errors safely', () => {
    jobServiceMock.getJob.mockReturnValue(throwError(() => ({ status: 500 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Job not found.');
  });
});
