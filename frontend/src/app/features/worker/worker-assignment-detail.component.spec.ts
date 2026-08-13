import { TestBed, ComponentFixture } from '@angular/core/testing';
import { WorkerAssignmentDetailComponent } from './worker-assignment-detail.component';
import { WorkerJobService } from './worker-job.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkerAssignment } from '../../core/models/worker.models';

describe('WorkerAssignmentDetailComponent', () => {
  let component: WorkerAssignmentDetailComponent;
  let fixture: ComponentFixture<WorkerAssignmentDetailComponent>;
  let workerJobServiceMock: any;

  const mockAssignment: WorkerAssignment = {
    assignmentId: 'wassign-200',
    jobId: 'fjob-400',
    jobTitle: 'Rice Field Sowing',
    workCategory: 'Sowing',
    wagePerDay: 800,
    farmerName: 'Dinesh Farm',
    farmLocation: 'Kheda Farm',
    workingHours: '7 AM - 4 PM',
    startDate: '2026-08-22',
    endDate: '2026-08-28',
    assignedAtUtc: '2026-08-14T02:00:00Z',
    status: 'Active'
  };

  beforeEach(async () => {
    workerJobServiceMock = {
      getAssignmentDetails: vi.fn().mockReturnValue(of(mockAssignment))
    };

    await TestBed.configureTestingModule({
      imports: [WorkerAssignmentDetailComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: WorkerJobService, useValue: workerJobServiceMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'wassign-200' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerAssignmentDetailComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load and display assignment details', () => {
    fixture.detectChanges();

    expect(workerJobServiceMock.getAssignmentDetails).toHaveBeenCalledWith('wassign-200');
    expect(component.loading()).toBe(false);
    expect(component.assignment()).toEqual(mockAssignment);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Rice Field Sowing');
    expect(compiled.textContent).toContain('Dinesh Farm');
    expect(compiled.textContent).toContain('₹800');
  });

  it('should handle assignment not found error', () => {
    workerJobServiceMock.getAssignmentDetails.mockReturnValue(throwError(() => ({ status: 404 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Assignment not found.');
  });
});
