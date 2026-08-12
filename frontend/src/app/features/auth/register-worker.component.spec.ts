import { TestBed, ComponentFixture } from '@angular/core/testing';
import { RegisterWorkerComponent } from './register-worker.component';
import { AuthService } from '../../core/services/auth.service';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { vi } from 'vitest';

describe('RegisterWorkerComponent', () => {
  let component: RegisterWorkerComponent;
  let fixture: ComponentFixture<RegisterWorkerComponent>;
  let authServiceMock: any;
  let router: Router;

  beforeEach(async () => {
    authServiceMock = {
      registerWorker: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [RegisterWorkerComponent, NoopAnimationsModule],
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        provideRouter([
          { path: 'auth/login', loadComponent: () => Promise.resolve(RegisterWorkerComponent) }
        ])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterWorkerComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should validate experience and expected wage as non-negative', () => {
    const form = component.registerForm;
    
    form.get('experienceYears')?.setValue(-1);
    form.get('expectedDailyWage')?.setValue(-100);
    
    expect(form.get('experienceYears')?.hasError('min')).toBe(true);
    expect(form.get('expectedDailyWage')?.hasError('min')).toBe(true);

    form.get('experienceYears')?.setValue(0);
    form.get('expectedDailyWage')?.setValue(0);

    expect(form.get('experienceYears')?.hasError('min')).toBe(false);
    expect(form.get('expectedDailyWage')?.hasError('min')).toBe(false);
  });

  it('should call AuthService.registerWorker and redirect to login on success', () => {
    vi.useFakeTimers();
    const mockResponse = {
      workerId: '1',
      email: 'worker@test.com',
      fullName: 'Worker Jane',
      message: 'Success'
    };
    authServiceMock.registerWorker.mockReturnValue(of(mockResponse));
    const navigateSpy = vi.spyOn(router, 'navigate');

    component.registerForm.patchValue({
      fullName: 'Worker Jane',
      email: 'worker@test.com',
      password: 'Password123!',
      confirmPassword: 'Password123!',
      phone: '1234567890',
      address: '123 Worker Lane',
      experienceYears: 5,
      expectedDailyWage: 450
    });

    expect(component.registerForm.valid).toBe(true);

    component.onSubmit();
    
    expect(authServiceMock.registerWorker).toHaveBeenCalled();
    vi.advanceTimersByTime(2000);
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
    vi.useRealTimers();
  });
});
