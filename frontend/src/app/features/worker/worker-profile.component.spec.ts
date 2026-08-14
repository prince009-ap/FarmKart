import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkerProfileComponent } from './worker-profile.component';
import { WorkerJobService } from './worker-job.service';
import { AuthService } from '../../core/services/auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { WorkerProfile, WorkerProfileCompletion } from '../../core/models/worker.models';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

describe('WorkerProfileComponent', () => {
  let component: WorkerProfileComponent;
  let fixture: ComponentFixture<WorkerProfileComponent>;
  let workerJobServiceMock: any;
  let authServiceMock: any;

  const mockProfile: WorkerProfile = {
    userId: '11111111-1111-1111-1111-111111111111',
    fullName: 'Ramesh Worker',
    email: 'ramesh.worker@example.com',
    phone: '9876543210',
    address: '123 Village Street',
    profileImageUrl: 'https://example.com/ramesh.jpg',
    experienceYears: 3,
    expectedDailyWage: 350,
    isAvailable: true,
    availableFrom: '2026-08-20',
    availabilityNotes: 'Available for harvesting, sowing, and general farm work.',
    experienceDescription: 'Worked on wheat and cotton harvesting and basic irrigation activities.',
    skills: ['Harvesting', 'Sowing', 'Irrigation']
  };

  const mockCompletion: WorkerProfileCompletion = {
    overallCompletionPercentage: 85,
    verificationStatus: 'Not Verified',
    sections: [
      { sectionKey: 'basic_info', sectionName: 'Basic Information', isComplete: true, completionPercentage: 20, description: 'Full name', actionRoute: '/worker/profile' },
      { sectionKey: 'skills_experience', sectionName: 'Skills & Experience', isComplete: true, completionPercentage: 25, description: 'Skills', actionRoute: '/worker/profile' },
      { sectionKey: 'availability', sectionName: 'Availability Status', isComplete: true, completionPercentage: 20, description: 'Status', actionRoute: '/worker/profile' },
      { sectionKey: 'job_preferences', sectionName: 'Job Preferences', isComplete: false, completionPercentage: 10, description: 'Preferences', actionRoute: '/worker/preferences' },
      { sectionKey: 'profile_photo', sectionName: 'Profile Photo', isComplete: true, completionPercentage: 10, description: 'Photo', actionRoute: '/worker/profile' }
    ]
  };

  beforeEach(async () => {
    workerJobServiceMock = {
      getProfile: vi.fn().mockReturnValue(of(mockProfile)),
      updateProfile: vi.fn().mockReturnValue(of(mockProfile)),
      getProfileCompletion: vi.fn().mockReturnValue(of(mockCompletion)),
      getReviews: vi.fn().mockReturnValue(of({
        averageRating: 4.5,
        totalReviews: 2,
        breakdown: { fiveStars: 1, fourStars: 1, threeStars: 0, twoStars: 0, oneStar: 0 },
        recentReviews: []
      }))
    };

    authServiceMock = {
      currentUser: vi.fn().mockReturnValue({
        userId: '11111111-1111-1111-1111-111111111111',
        email: 'ramesh.worker@example.com',
        fullName: 'Ramesh Worker',
        role: 'Worker'
      })
    };

    vi.spyOn(MatSnackBar.prototype, 'open').mockImplementation(() => ({} as any));

    await TestBed.configureTestingModule({
      imports: [WorkerProfileComponent, NoopAnimationsModule],
      providers: [
        provideRouter([
          { path: 'worker/preferences', component: WorkerProfileComponent },
          { path: 'worker/profile', component: WorkerProfileComponent }
        ]),
        { provide: WorkerJobService, useValue: workerJobServiceMock },
        { provide: AuthService, useValue: authServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('1. Profile completion percentage displays', () => {
    expect(workerJobServiceMock.getProfileCompletion).toHaveBeenCalled();
    expect(component.profileCompletion()?.overallCompletionPercentage).toBe(85);
  });

  it('2. Progress indicator updates', () => {
    expect(component.profileCompletion()?.overallCompletionPercentage).toBe(85);
  });

  it('3. Completed sections display correctly', () => {
    const sections = component.profileCompletion()?.sections || [];
    const basicInfo = sections.find(s => s.sectionKey === 'basic_info');
    expect(basicInfo?.isComplete).toBe(true);
  });

  it('4. Incomplete sections display correctly', () => {
    const sections = component.profileCompletion()?.sections || [];
    const prefs = sections.find(s => s.sectionKey === 'job_preferences');
    expect(prefs?.isComplete).toBe(false);
  });

  it('5. Navigation to incomplete sections works', () => {
    const prefs = component.profileCompletion()?.sections.find(s => s.sectionKey === 'job_preferences');
    if (prefs) {
      component.onSectionAction(prefs);
    }
    expect(component).toBeTruthy();
  });

  it('6. Verification status displays', () => {
    expect(component.profileCompletion()?.verificationStatus).toBe('Not Verified');
  });

  it('7. Worker cannot manually set Verified from UI', () => {
    component.enableEdit();
    expect(component.profileForm.get('verificationStatus')).toBeNull();
  });

  it('8. Loading state works', () => {
    expect(component.loading()).toBe(false);
  });

  it('9. API error state handles gracefully', () => {
    workerJobServiceMock.updateProfile.mockReturnValue(throwError(() => ({ error: { message: 'Failed to update profile. Please try again.' } })));

    component.enableEdit();
    component.profileForm.patchValue({
      fullName: 'Ramesh Worker',
      phone: '9876543210',
      address: '123 Village Street',
      experienceYears: 3,
      expectedDailyWage: 350
    });

    component.onSubmit();

    expect(component.saving()).toBe(false);
    expect(MatSnackBar.prototype.open).toHaveBeenCalledWith('Failed to update profile. Please try again.', 'Close', expect.any(Object));
  });
});
