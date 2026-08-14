import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { WorkerProfileComponent } from './worker-profile.component';
import { WorkerJobService } from './worker-job.service';
import { WorkerProfile } from '../../core/models/worker.models';

describe('WorkerProfileComponent', () => {
  let component: WorkerProfileComponent;
  let fixture: ComponentFixture<WorkerProfileComponent>;
  let workerServiceMock: any;

  const mockProfile: WorkerProfile = {
    userId: 'user-123',
    fullName: 'Yash Sarvaiya',
    email: 'worker@test.com',
    phone: '9876543210',
    address: '123 Farm Street',
    profileImageUrl: 'https://example.com/avatar.jpg',
    experienceYears: 3,
    expectedDailyWage: 500,
    isAvailable: true
  };

  beforeEach(async () => {
    workerServiceMock = {
      getProfile: vi.fn().mockReturnValue(of(mockProfile)),
      updateProfile: vi.fn().mockReturnValue(of({ ...mockProfile, fullName: 'Updated Yash Sarvaiya' }))
    };

    await TestBed.configureTestingModule({
      imports: [WorkerProfileComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: WorkerJobService, useValue: workerServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkerProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load existing worker data', () => {
    expect(component).toBeTruthy();
    expect(component.loading()).toBe(false);
    expect(component.profile()).toEqual(mockProfile);
    expect(workerServiceMock.getProfile).toHaveBeenCalled();
  });

  it('should display existing worker information', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Yash Sarvaiya');
    expect(compiled.textContent).toContain('worker@test.com');
    expect(compiled.textContent).toContain('9876543210');
  });

  it('should toggle edit mode when Edit Profile button is clicked', () => {
    expect(component.editMode()).toBe(false);
    component.enableEdit();
    fixture.detectChanges();
    expect(component.editMode()).toBe(true);

    const compiled = fixture.nativeElement as HTMLElement;
    const emailInput = compiled.querySelector('input[type="email"]') as HTMLInputElement;
    expect(emailInput).toBeTruthy();
    expect(emailInput.disabled).toBe(true);
  });

  it('should cancel edit and restore previous form values', () => {
    component.enableEdit();
    component.profileForm.patchValue({ fullName: 'Temporary Name' });
    expect(component.profileForm.value.fullName).toBe('Temporary Name');

    component.cancelEdit();
    expect(component.editMode()).toBe(false);
    expect(component.profileForm.value.fullName).toBe('Yash Sarvaiya');
  });

  it('should display validation errors for invalid inputs', () => {
    component.enableEdit();
    component.profileForm.patchValue({ fullName: '', phone: 'invalid', experienceYears: -5 });
    component.onSubmit();
    fixture.detectChanges();

    expect(component.profileForm.invalid).toBe(true);
    expect(workerServiceMock.updateProfile).not.toHaveBeenCalled();
  });

  it('should update profile when valid form is submitted', () => {
    component.enableEdit();
    component.profileForm.patchValue({
      fullName: 'Updated Yash Sarvaiya',
      phone: '9998887776',
      address: '456 New Road',
      experienceYears: 4,
      expectedDailyWage: 600
    });

    component.onSubmit();
    expect(workerServiceMock.updateProfile).toHaveBeenCalledWith(expect.objectContaining({
      fullName: 'Updated Yash Sarvaiya',
      phone: '9998887776',
      address: '456 New Road',
      experienceYears: 4,
      expectedDailyWage: 600
    }));

    expect(component.editMode()).toBe(false);
    expect(component.successMessage()).toContain('Profile updated successfully');
  });

  it('should handle profile load error state', () => {
    workerServiceMock.getProfile.mockReturnValue(throwError(() => ({ status: 404 })));
    component.loadProfile();
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.loadError()).toBe('Worker profile not found.');
  });
});
