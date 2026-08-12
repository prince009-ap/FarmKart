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

  it('should validate required fields except optional farm name', () => {
    const form = component.registerForm;
    expect(form.valid).toBe(false);

    expect(form.get('fullName')?.hasError('required')).toBe(true);
    expect(form.get('email')?.hasError('required')).toBe(true);
    expect(form.get('password')?.hasError('required')).toBe(true);
    expect(form.get('confirmPassword')?.hasError('required')).toBe(true);
    expect(form.get('phone')?.hasError('required')).toBe(true);
    expect(form.get('address')?.hasError('required')).toBe(true);
    expect(form.get('farmName')?.hasError('required')).toBeFalsy();
    expect(form.get('farmSize')?.hasError('required')).toBe(true);
    expect(form.get('farmSizeUnit')?.hasError('required')).toBeFalsy();
    expect(form.get('farmSizeUnit')?.value).toBe('Vigha');
  });

  it('should default farm size unit to Vigha and expose unit options', () => {
    expect(component.registerForm.get('farmSizeUnit')?.value).toBe('Vigha');
    expect(component.farmSizeUnitOptions).toEqual(['Vigha', 'Acre', 'Hectare']);
    expect(fixture.nativeElement.querySelector('mat-select[formcontrolname="farmSizeUnit"]')).toBeTruthy();
  });

  it('should reject negative farm size', () => {
    const farmSizeControl = component.registerForm.get('farmSize');
    farmSizeControl?.setValue(-1);
    expect(farmSizeControl?.hasError('min')).toBe(true);
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
      farmSizeUnit: 'Vigha',
      farmLocation: 'Valley Description'
    });

    expect(component.registerForm.valid).toBe(true);

    component.onSubmit();
    
    expect(authServiceMock.registerFarmer).toHaveBeenCalled();
    
    const callArgs = authServiceMock.registerFarmer.mock.calls[0][0];
    expect(callArgs.latitude).toBeUndefined();
    expect(callArgs.longitude).toBeUndefined();
    expect(callArgs.city).toBeUndefined();
    expect(callArgs.state).toBeUndefined();
    expect(callArgs.pincode).toBeUndefined();
    expect(callArgs.address).toBe('123 Farm Lane');
    expect(callArgs.farmSize).toBe(15);
    expect(callArgs.farmSizeUnit).toBe('Vigha');

    vi.advanceTimersByTime(2000);
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
    vi.useRealTimers();
  });

  it('should send selected Acre unit to the backend', () => {
    authServiceMock.registerFarmer.mockReturnValue(of({
      farmerId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      message: 'Success'
    }));

    component.registerForm.patchValue({
      fullName: 'Farmer John',
      email: 'farmer@test.com',
      password: 'Password123!',
      confirmPassword: 'Password123!',
      phone: '1234567890',
      address: '123 Farm Lane',
      farmSize: 10,
      farmSizeUnit: 'Acre'
    });

    component.onSubmit();

    const callArgs = authServiceMock.registerFarmer.mock.calls[0][0];
    expect(callArgs.farmSizeUnit).toBe('Acre');
  });

  it('should send selected Hectare unit to the backend', () => {
    authServiceMock.registerFarmer.mockReturnValue(of({
      farmerId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      message: 'Success'
    }));

    component.registerForm.patchValue({
      fullName: 'Farmer John',
      email: 'farmer@test.com',
      password: 'Password123!',
      confirmPassword: 'Password123!',
      phone: '1234567890',
      address: '123 Farm Lane',
      farmSize: 8,
      farmSizeUnit: 'Hectare'
    });

    component.onSubmit();

    const callArgs = authServiceMock.registerFarmer.mock.calls[0][0];
    expect(callArgs.farmSizeUnit).toBe('Hectare');
  });

  it('should send null farm name when left blank', () => {
    authServiceMock.registerFarmer.mockReturnValue(of({
      farmerId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      message: 'Success'
    }));

    component.registerForm.patchValue({
      fullName: 'Farmer John',
      email: 'farmer@test.com',
      password: 'Password123!',
      confirmPassword: 'Password123!',
      phone: '1234567890',
      address: '123 Farm Lane',
      farmName: '',
      farmSize: 5,
      farmSizeUnit: 'Vigha',
      farmLocation: null
    });

    component.onSubmit();

    const callArgs = authServiceMock.registerFarmer.mock.calls[0][0];
    expect(callArgs.farmName).toBeNull();
    expect(callArgs.farmSizeUnit).toBe('Vigha');
  });
});
