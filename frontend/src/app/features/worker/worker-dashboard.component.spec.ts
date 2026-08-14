import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkerDashboardComponent } from './worker-dashboard.component';
import { WorkerJobService } from './worker-job.service';
import { AuthService } from '../../core/services/auth.service';
import { of } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

describe('WorkerDashboardComponent', () => {
  let component: WorkerDashboardComponent;
  let fixture: ComponentFixture<WorkerDashboardComponent>;
  let mockWorkerService: any;
  let mockAuthService: any;

  beforeEach(async () => {
    mockWorkerService = {
      getMyApplications: vi.fn().mockReturnValue(of([])),
      getMyAssignments: vi.fn().mockReturnValue(of([])),
      getEarnings: vi.fn().mockReturnValue(of({ thisMonthEarnings: 1500, totalEarnings: 1500 })),
      getMyAttendance: vi.fn().mockReturnValue(of({ attendancePercentage: 90 })),
      getProfileCompletion: vi.fn().mockReturnValue(of({ overallCompletionPercentage: 85 })),
      getNotifications: vi.fn().mockReturnValue(of([]))
    };

    mockAuthService = {
      currentUser$: of({ fullName: 'Yash Worker', email: 'yash@test.com', role: 'Worker' })
    };

    await TestBed.configureTestingModule({
      imports: [WorkerDashboardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        provideRouter([]),
        { provide: WorkerJobService, useValue: mockWorkerService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('1. Dashboard loads successfully', () => {
    expect(component).toBeTruthy();
    expect(component.userName()).toBe('Yash Worker');
  });

  it('2. Live metrics populate from services', () => {
    expect(component.monthlyEarnings()).toBe(1500);
    expect(component.attendanceRate()).toBe(90);
    expect(component.profileCompletion()?.overallCompletionPercentage).toBe(85);
  });
});
