import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkerProfileComponent } from './worker-profile.component';
import { WorkerJobService } from './worker-job.service';
import { AuthService } from '../../core/services/auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { WorkerProfile } from '../../core/models/worker.models';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

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

  beforeEach(async () => {
    workerJobServiceMock = {
      getProfile: vi.fn().mockReturnValue(of(mockProfile)),
      updateProfile: vi.fn().mockReturnValue(of(mockProfile))
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
        { provide: WorkerJobService, useValue: workerJobServiceMock },
        { provide: AuthService, useValue: authServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('1. Availability status loads correctly', () => {
    expect(workerJobServiceMock.getProfile).toHaveBeenCalled();
    expect(component.profile()?.isAvailable).toBe(true);
    expect(component.profileForm.get('isAvailable')?.value).toBe(true);
  });

  it('2. Worker can toggle availability', () => {
    component.enableEdit();
    component.toggleAvailability(false);

    expect(component.profileForm.get('isAvailable')?.value).toBe(false);
    expect(component.profileForm.get('availableFrom')?.value).toBe('');
  });

  it('3. AvailableFrom loads correctly', () => {
    expect(component.profile()?.availableFrom).toBe('2026-08-20');
    expect(component.profileForm.get('availableFrom')?.value).toBe('2026-08-20');
  });

  it('4. Availability notes load correctly', () => {
    expect(component.profile()?.availabilityNotes).toBe('Available for harvesting, sowing, and general farm work.');
    expect(component.profileForm.get('availabilityNotes')?.value).toBe('Available for harvesting, sowing, and general farm work.');
  });

  it('5. Changes can be saved', () => {
    component.enableEdit();
    component.profileForm.patchValue({
      isAvailable: false,
      availabilityNotes: 'Currently unavailable due to personal leave'
    });

    component.onSubmit();

    expect(workerJobServiceMock.updateProfile).toHaveBeenCalledWith(expect.objectContaining({
      isAvailable: false,
      availabilityNotes: 'Currently unavailable due to personal leave'
    }));
  });

  it('6. Validation errors display', () => {
    component.enableEdit();
    const notesControl = component.profileForm.get('availabilityNotes');
    notesControl?.setValue('a'.repeat(501)); // Exceed max 500 length
    notesControl?.markAsTouched();

    fixture.detectChanges();

    expect(notesControl?.hasError('maxlength')).toBe(true);
    expect(component.profileForm.invalid).toBe(true);
  });

  it('7. Success state works on submit', () => {
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
    expect(component.editMode()).toBe(false);
    expect(component.successMessage()).toBe('Profile updated successfully.');
  });

  it('8. API error state handles gracefully', () => {
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
