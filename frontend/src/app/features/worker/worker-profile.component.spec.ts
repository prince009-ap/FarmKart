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
  let snackBar: MatSnackBar;

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
    availableFrom: undefined,
    availabilityNotes: undefined,
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

    await TestBed.configureTestingModule({
      imports: [WorkerProfileComponent, NoopAnimationsModule],
      providers: [
        { provide: WorkerJobService, useValue: workerJobServiceMock },
        { provide: AuthService, useValue: authServiceMock }
      ]
    }).compileComponents();

    snackBar = TestBed.inject(MatSnackBar);
    vi.spyOn(snackBar, 'open').mockImplementation(() => ({} as any));

    fixture = TestBed.createComponent(WorkerProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('1. Skills load correctly', () => {
    expect(workerJobServiceMock.getProfile).toHaveBeenCalled();
    expect(component.skills()).toEqual(['Harvesting', 'Sowing', 'Irrigation']);
  });

  it('2. Existing skills display', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Harvesting');
    expect(compiled.textContent).toContain('Sowing');
    expect(compiled.textContent).toContain('Irrigation');
  });

  it('3. Skill can be added', () => {
    component.enableEdit();
    component.newSkillInput.set('Crop Maintenance');
    component.addSkill();

    expect(component.skills()).toContain('Crop Maintenance');
    expect(component.skills().length).toBe(4);
    expect(component.newSkillInput()).toBe('');
  });

  it('4. Skill can be removed', () => {
    component.enableEdit();
    component.removeSkill(1); // remove 'Sowing'

    expect(component.skills()).toEqual(['Harvesting', 'Irrigation']);
    expect(component.skills().length).toBe(2);
  });

  it('5. Duplicate skill is prevented', () => {
    component.enableEdit();
    component.newSkillInput.set('harvesting'); // Case-insensitive duplicate
    component.addSkill();

    expect(component.skillError()).toBeTruthy();
    expect(component.skillError()).toContain('harvesting');
    expect(component.skills().length).toBe(3);
  });

  it('6. Experience can be edited', () => {
    component.enableEdit();
    component.profileForm.patchValue({ experienceYears: 5 });

    expect(component.profileForm.value.experienceYears).toBe(5);
  });

  it('7. Experience description can be edited', () => {
    component.enableEdit();
    component.profileForm.patchValue({ experienceDescription: 'Experienced in operating tractors.' });

    expect(component.profileForm.value.experienceDescription).toBe('Experienced in operating tractors.');
  });

  it('8. Validation messages display for negative experience and invalid phone', () => {
    component.enableEdit();
    const expControl = component.profileForm.get('experienceYears');
    expControl?.setValue(-2);
    expControl?.markAsTouched();

    const phoneControl = component.profileForm.get('phone');
    phoneControl?.setValue('invalid');
    phoneControl?.markAsTouched();

    fixture.detectChanges();

    expect(expControl?.hasError('min')).toBe(true);
    expect(phoneControl?.hasError('pattern')).toBe(true);
    expect(component.profileForm.invalid).toBe(true);
  });

  it('9. Save calls correct API with skills and experience description', () => {
    component.enableEdit();
    component.profileForm.setValue({
      fullName: 'Ramesh Worker Updated',
      phone: '9876543210',
      address: '456 New Road',
      experienceYears: 5,
      experienceDescription: '5 years of tractor driving and crop harvesting experience.',
      expectedDailyWage: 400,
      profileImageUrl: ''
    });
    component.skills.set(['Harvesting', 'Tractor Operation']);

    component.onSubmit();

    expect(workerJobServiceMock.updateProfile).toHaveBeenCalledWith(expect.objectContaining({
      fullName: 'Ramesh Worker Updated',
      experienceYears: 5,
      experienceDescription: '5 years of tractor driving and crop harvesting experience.',
      skills: ['Harvesting', 'Tractor Operation']
    }));
  });

  it('10. Success state works on submit', () => {
    component.enableEdit();
    component.profileForm.setValue({
      fullName: 'Ramesh Worker',
      phone: '9876543210',
      address: '123 Village Street',
      experienceYears: 3,
      experienceDescription: 'Worked on wheat harvesting.',
      expectedDailyWage: 350,
      profileImageUrl: ''
    });

    expect(component.profileForm.valid).toBe(true);
    component.onSubmit();
    expect(workerJobServiceMock.updateProfile).toHaveBeenCalled();
    expect(component.saving()).toBe(false);
    expect(component.editMode()).toBe(false);
    expect(component.successMessage()).toBe('Profile updated successfully.');
  });

  it('11. API error state handles gracefully', () => {
    workerJobServiceMock.updateProfile.mockReturnValue(throwError(() => ({ error: { message: 'Server error' } })));
    component.enableEdit();
    component.profileForm.setValue({
      fullName: 'Ramesh Worker',
      phone: '9876543210',
      address: '123 Village Street',
      experienceYears: 3,
      experienceDescription: 'Worked on wheat harvesting.',
      expectedDailyWage: 350,
      profileImageUrl: ''
    });

    expect(component.profileForm.valid).toBe(true);
    component.onSubmit();
    expect(workerJobServiceMock.updateProfile).toHaveBeenCalled();
    expect(component.saving()).toBe(false);
  });
});
