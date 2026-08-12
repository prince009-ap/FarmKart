import { TestBed, ComponentFixture } from '@angular/core/testing';
import { RegisterFarmerComponent } from './register-farmer.component';
import { AuthService } from '../../core/services/auth.service';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { vi } from 'vitest';

describe('RegisterFarmerComponent', () => {
  let component: RegisterFarmerComponent;
  let fixture: ComponentFixture<RegisterFarmerComponent>;
  let authServiceMock: any;
  let router: Router;

  beforeEach(async () => {
    authServiceMock = {
      registerFarmer: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [RegisterFarmerComponent, NoopAnimationsModule],
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        provideRouter([
          { path: 'auth/login', loadComponent: () => Promise.resolve(RegisterFarmerComponent) }
        ])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterFarmerComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should validate form fields as required', () => {
    const form = component.registerForm;
    expect(form.valid).toBe(false);

    expect(form.get('fullName')?.hasError('required')).toBe(true);
    expect(form.get('email')?.hasError('required')).toBe(true);
    expect(form.get('password')?.hasError('required')).toBe(true);
    expect(form.get('confirmPassword')?.hasError('required')).toBe(true);
    expect(form.get('phone')?.hasError('required')).toBe(true);
    expect(form.get('address')?.hasError('required')).toBe(true);
    expect(form.get('farmName')?.hasError('required')).toBe(true);
    expect(form.get('farmSize')?.hasError('required')).toBe(true);
  });

  it('should reject password mismatch', () => {
    const form = component.registerForm;
    form.get('password')?.setValue('Password123!');
    form.get('confirmPassword')?.setValue('PasswordMismatch!');
    expect(form.hasError('passwordMismatch')).toBe(true);
  });

  it('should call AuthService.registerFarmer and redirect to login on success', () => {
    vi.useFakeTimers();
    const mockResponse = {
      farmerId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      message: 'Success'
    };
    authServiceMock.registerFarmer.mockReturnValue(of(mockResponse));
    const navigateSpy = vi.spyOn(router, 'navigate');

    component.registerForm.patchValue({
      fullName: 'Farmer John',
      email: 'farmer@test.com',
      password: 'Password123!',
      confirmPassword: 'Password123!',
      phone: '1234567890',
      address: '123 Farm Lane',
      farmName: 'Valley Farms',
      farmSize: 15,
      farmLocation: 'Valley Description'
    });

    expect(component.registerForm.valid).toBe(true);

    component.onSubmit();
    
    expect(authServiceMock.registerFarmer).toHaveBeenCalled();
    
    // Check parameters to ensure no lat/lng/city/state/pincode are included in DTO
    const callArgs = authServiceMock.registerFarmer.mock.calls[0][0];
    expect(callArgs.latitude).toBeUndefined();
    expect(callArgs.longitude).toBeUndefined();
    expect(callArgs.city).toBeUndefined();
    expect(callArgs.state).toBeUndefined();
    expect(callArgs.pincode).toBeUndefined();
    expect(callArgs.address).toBe('123 Farm Lane');

    vi.advanceTimersByTime(2000);
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
    vi.useRealTimers();
  });
});
