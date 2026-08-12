import { TestBed, ComponentFixture } from '@angular/core/testing';
import { RegisterCustomerComponent } from './register-customer.component';
import { AuthService } from '../../core/services/auth.service';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { vi } from 'vitest';

describe('RegisterCustomerComponent', () => {
  let component: RegisterCustomerComponent;
  let fixture: ComponentFixture<RegisterCustomerComponent>;
  let authServiceMock: any;
  let router: Router;

  beforeEach(async () => {
    authServiceMock = {
      registerCustomer: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [RegisterCustomerComponent, NoopAnimationsModule],
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        provideRouter([
          { path: 'auth/login', loadComponent: () => Promise.resolve(RegisterCustomerComponent) }
        ])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterCustomerComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should call AuthService.registerCustomer and redirect to login on success', () => {
    vi.useFakeTimers();
    const mockResponse = {
      customerId: '1',
      email: 'customer@test.com',
      fullName: 'Customer Alice',
      message: 'Success'
    };
    authServiceMock.registerCustomer.mockReturnValue(of(mockResponse));
    const navigateSpy = vi.spyOn(router, 'navigate');

    component.registerForm.patchValue({
      fullName: 'Customer Alice',
      email: 'customer@test.com',
      password: 'Password123!',
      confirmPassword: 'Password123!',
      phone: '1234567890',
      address: '123 Customer Lane'
    });

    expect(component.registerForm.valid).toBe(true);

    component.onSubmit();
    
    expect(authServiceMock.registerCustomer).toHaveBeenCalled();
    vi.advanceTimersByTime(2000);
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
    vi.useRealTimers();
  });
});
