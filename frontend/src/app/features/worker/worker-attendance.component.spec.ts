import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { WorkerAttendanceComponent } from './worker-attendance.component';
import { WorkerJobService } from './worker-job.service';
import { WorkerAttendanceSummary } from '../../core/models/worker.models';

describe('WorkerAttendanceComponent', () => {
  let component: WorkerAttendanceComponent;
  let fixture: ComponentFixture<WorkerAttendanceComponent>;
  let jobServiceMock: any;

  const mockSummary: WorkerAttendanceSummary = {
    totalDays: 2,
    presentDays: 2,
    absentDays: 0,
    halfDays: 0,
    leaveDays: 0,
    attendancePercentage: 100,
    history: [
      {
        attendanceId: 'att-1',
        workerAssignmentId: 'assign-1',
        jobId: 'job-1',
        jobTitle: 'Wheat Harvesting',
        farmerName: 'Ramesh Patel',
        date: '2026-08-20',
        status: 'Present',
        notes: 'Full day work',
        totalHours: 8
      }
    ]
  };

  beforeEach(async () => {
    jobServiceMock = {
      getMyAttendance: vi.fn().mockReturnValue(of(mockSummary)),
      getAssignmentAttendance: vi.fn().mockReturnValue(of(mockSummary))
    };

    await TestBed.configureTestingModule({
      imports: [WorkerAttendanceComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: WorkerJobService, useValue: jobServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerAttendanceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load worker attendance summary and history', () => {
    expect(component).toBeTruthy();
    expect(component.summary()?.totalDays).toBe(2);
    expect(component.summary()?.presentDays).toBe(2);
    expect(component.summary()?.attendancePercentage).toBe(100);
    expect(component.summary()?.history.length).toBe(1);
  });

  it('should handle error when loading attendance history fails', () => {
    jobServiceMock.getMyAttendance.mockReturnValue(throwError(() => new Error('Server error')));
    component.loadAllAttendance();
    expect(component.error()).toBe('Unable to load attendance records.');
  });
});
