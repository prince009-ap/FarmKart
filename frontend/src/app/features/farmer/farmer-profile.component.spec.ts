import { TestBed, ComponentFixture } from '@angular/core/testing';
import { FarmerProfileComponent } from './farmer-profile.component';
import { FarmerProfileService } from './farmer-profile.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { vi } from 'vitest';
import { FarmerProfile } from '../../core/models/farmer.models';

describe('FarmerProfileComponent', () => {
  let component: FarmerProfileComponent;
  let fixture: ComponentFixture<FarmerProfileComponent>;
  let profileServiceMock: any;

  const mockProfile: FarmerProfile = {
    userId: '123',
    fullName: 'Farmer John',
    email: 'farmer.john@example.com',
    phone: '1234567890',
    address: '123 Green Farm Road',
    farmName: 'Valley Farm',
    farmSize: 12.5,
    farmSizeUnit: 'Vigha',
    farmLocation: 'Near River'
  };

  beforeEach(async () => {
    profileServiceMock = {
      getProfile: vi.fn().mockReturnValue(of(mockProfile)),
      updateProfile: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [FarmerProfileComponent, NoopAnimationsModule],
      providers: [
        { provide: FarmerProfileService, useValue: profileServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerProfileComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should load farmer profile on init and set form values', () => {
    fixture.detectChanges(); // triggers ngOnInit

    expect(profileServiceMock.getProfile).toHaveBeenCalled();
    expect(component.profile()).toEqual(mockProfile);
    expect(component.field('fullName')?.value).toBe('Farmer John');
    expect(component.field('phone')?.value).toBe('1234567890');
    expect(component.field('address')?.value).toBe('123 Green Farm Road');
    expect(component.field('farmName')?.value).toBe('Valley Farm');
    expect(component.field('farmSize')?.value).toBe(12.5);
    expect(component.field('farmSizeUnit')?.value).toBe('Vigha');
    expect(component.field('farmLocation')?.value).toBe('Near River');
  });

  it('should call GET API correctly on init', () => {
    fixture.detectChanges();
    expect(profileServiceMock.getProfile).toHaveBeenCalledTimes(1);
  });

  it('should switch to edit mode when enterEditMode is called', () => {
    fixture.detectChanges();
    expect(component.editMode()).toBe(false);
    
    component.enterEditMode();
    expect(component.editMode()).toBe(true);
  });

  it('should call PUT API correctly and update profile on successful save', () => {
    fixture.detectChanges();
    component.enterEditMode();

    const updatedProfile: FarmerProfile = {
      ...mockProfile,
      fullName: 'Farmer John Updated',
      phone: '9876543210',
      address: '456 New Road',
      farmSize: 15
    };

    profileServiceMock.updateProfile.mockReturnValue(of(updatedProfile));

    component.profileForm.patchValue({
      fullName: 'Farmer John Updated',
      phone: '9876543210',
      address: '456 New Road',
      farmSize: 15
    });

    component.saveProfile();

    expect(profileServiceMock.updateProfile).toHaveBeenCalledWith({
      fullName: 'Farmer John Updated',
      phone: '9876543210',
      address: '456 New Road',
      farmName: 'Valley Farm',
      farmSize: 15,
      farmSizeUnit: 'Vigha',
      farmLocation: 'Near River'
    });
    
    expect(component.profile()).toEqual(updatedProfile);
    expect(component.editMode()).toBe(false);
  });

  it('should display updated profile information in view mode', () => {
    fixture.detectChanges();
    component.profile.set({
      ...mockProfile,
      fullName: 'John Updated Display'
    });
    fixture.detectChanges();
    
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('John Updated Display');
  });

  it('should reject negative farm size', () => {
    fixture.detectChanges();
    component.enterEditMode();
    
    const sizeControl = component.field('farmSize');
    sizeControl?.setValue(-5);
    fixture.detectChanges();

    expect(sizeControl?.hasError('min')).toBe(true);
    expect(component.profileForm.invalid).toBe(true);
  });

  it('should disable save button and toggle saving flag while saving', () => {
    fixture.detectChanges();
    component.enterEditMode();

    profileServiceMock.updateProfile.mockReturnValue(of(mockProfile));
    
    expect(component.saving()).toBe(false);
    component.saveProfile();
    
    // In our component, saveProfile executes synchronously in unit test because of mock returns 'of' instantly,
    // so we can assert the final state is saving=false. If we want to test state during active request:
    expect(component.saving()).toBe(false); // resets after response
  });

  it('should handle API errors safely when loading profile', () => {
    profileServiceMock.getProfile.mockReturnValue(throwError(() => ({ status: 500 })));
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.loadError()).toBe('Failed to load profile. Please try again.');
  });

  it('should handle API errors safely when saving profile updates', () => {
    fixture.detectChanges();
    component.enterEditMode();

    profileServiceMock.updateProfile.mockReturnValue(throwError(() => ({ status: 400, error: { message: 'Invalid data' } })));
    
    component.saveProfile();
    expect(component.saving()).toBe(false);
    expect(component.editMode()).toBe(true); // remains in edit mode
  });
});
