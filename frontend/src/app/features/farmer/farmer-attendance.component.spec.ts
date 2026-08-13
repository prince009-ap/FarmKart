import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { FarmerAttendanceComponent } from './farmer-attendance.component';
import { FarmerJobService } from './farmer-job.service';
import { FarmerAttendanceRecord, FarmerJob, FarmerWorkerAssignment } from '../../core/models/farmer.models';

describe('FarmerAttendanceComponent', () => {
  let component: FarmerAttendanceComponent;
  let fixture: ComponentFixture<FarmerAttendanceComponent>;
  let jobServiceMock: any;

  const mockJob: FarmerJob = {
    id: 'job-1',
    title: 'Wheat Harvesting',
    description: 'Harvest wheat field',
    workCategory: 'Harvesting',
    cropType: 'Wheat',
    workersRequired: 3,
    requiredExperience: 1,
    wagePerDay: 500,
    startDate: '2026-08-01',
    endDate: '2026-08-25',
    workingHours: '8 AM - 5 PM',
    farmLocation: 'Green Valley',
    farmSize: 5,
    foodProvided: true,
    accommodationProvided: false,
    isUrgent: false,
    status: 'Open',
    createdAtUtc: '2026-08-01T00:00:00Z'
  };

  const mockAssignments: FarmerWorkerAssignment[] = [
    {
      assignmentId: 'assign-1',
      jobId: 'job-1',
      jobTitle: 'Wheat Harvesting',
      workerProfileId: 'worker-1',
      workerName: 'Yash Sarvaiya',
      workerPhone: '9876543210',
      workerExperienceYears: 2,
      workerSkills: ['Harvesting'],
      startDate: '2026-08-01',
      endDate: '2026-08-25',
      assignedAtUtc: '2026-08-01T00:00:00Z',
      status: 'Active'
    }
  ];

  const mockRecords: FarmerAttendanceRecord[] = [];

  beforeEach(async () => {
    jobServiceMock = {
      getJob: vi.fn().mockReturnValue(of(mockJob)),
      getJobAssignments: vi.fn().mockReturnValue(of(mockAssignments)),
      getJobAttendance: vi.fn().mockReturnValue(of(mockRecords)),
      saveJobAttendance: vi.fn().mockReturnValue(of(mockRecords))
    };

    await TestBed.configureTestingModule({
      imports: [FarmerAttendanceComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: FarmerJobService, useValue: jobServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'jobId' ? 'job-1' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerAttendanceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and initialize with unselected attendance status by default', () => {
    expect(component).toBeTruthy();
    expect(component.attendanceRows().length).toBe(1);
    expect(component.attendanceRows()[0].status).toBe('');
    expect(component.canSave).toBe(false);
  });

  it('should prevent save when status is not explicitly selected', () => {
    component.saveAttendance();
    expect(component.error()).toContain('Please select an attendance status');
    expect(jobServiceMock.saveJobAttendance).not.toHaveBeenCalled();
  });

  it('should allow save when a valid status is selected', () => {
    component.attendanceRows()[0].status = 'Present';
    expect(component.canSave).toBe(true);

    component.saveAttendance();
    expect(jobServiceMock.saveJobAttendance).toHaveBeenCalledWith('job-1', expect.objectContaining({
      items: expect.arrayContaining([
        expect.objectContaining({ workerAssignmentId: 'assign-1', status: 'Present' })
      ])
    }));
  });

  it('should show info banner and disable save if today is before job start date', () => {
    const futureJob: FarmerJob = { ...mockJob, startDate: '2029-12-01', endDate: '2029-12-31' };
    jobServiceMock.getJob.mockReturnValue(of(futureJob));

    component.loadData('job-1');
    fixture.detectChanges();

    expect(component.isBeforeStartDate()).toBe(true);
    expect(component.canSave).toBe(false);
  });
});
