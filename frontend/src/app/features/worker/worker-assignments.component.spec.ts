import { TestBed, ComponentFixture } from '@angular/core/testing';
import { WorkerAssignmentsComponent } from './worker-assignments.component';
import { WorkerJobService } from './worker-job.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkerAssignment } from '../../core/models/worker.models';

describe('WorkerAssignmentsComponent', () => {
  let component: WorkerAssignmentsComponent;
  let fixture: ComponentFixture<WorkerAssignmentsComponent>;
  let workerJobServiceMock: any;

  const mockAssignments: WorkerAssignment[] = [
    {
      assignmentId: 'wassign-1',
      jobId: 'fjob-300',
      jobTitle: 'Wheat Harvesting Task',
      workCategory: 'Harvesting',
      wagePerDay: 650,
      farmerName: 'Ramesh Patel',
      farmLocation: 'Anand Farm',
      workingHours: '8 AM - 5 PM',
      startDate: '2026-08-20',
      endDate: '2026-08-25',
      assignedAtUtc: '2026-08-14T01:00:00Z',
      status: 'Active'
    }
  ];

  beforeEach(async () => {
    workerJobServiceMock = {
      getMyAssignments: vi.fn().mockReturnValue(of(mockAssignments))
    };

    await TestBed.configureTestingModule({
      imports: [WorkerAssignmentsComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: WorkerJobService, useValue: workerJobServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerAssignmentsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load and render worker assignments', () => {
    fixture.detectChanges();

    expect(workerJobServiceMock.getMyAssignments).toHaveBeenCalled();
    expect(component.loading()).toBe(false);
    expect(component.assignments()).toEqual(mockAssignments);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Wheat Harvesting Task');
    expect(compiled.textContent).toContain('Ramesh Patel');
    expect(compiled.textContent).toContain('Active');
  });

  it('should display empty state when worker has no assignments', () => {
    workerJobServiceMock.getMyAssignments.mockReturnValue(of([]));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.assignments()).toEqual([]);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('No job assignments yet.');
  });

  it('should handle API error gracefully', () => {
    workerJobServiceMock.getMyAssignments.mockReturnValue(throwError(() => ({ status: 500 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Unable to load your job assignments.');
  });
});
