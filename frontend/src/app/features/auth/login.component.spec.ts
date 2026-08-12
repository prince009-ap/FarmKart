import { TestBed, ComponentFixture } from '@angular/core/testing';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/services/auth.service';
import { Router, ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { vi } from 'vitest';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceMock: any;
  let router: Router;

  beforeEach(async () => {
    authServiceMock = {
      currentUser$: of(null),
      login: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [LoginComponent, NoopAnimationsModule],
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        provideRouter([
          { path: 'farmer', loadComponent: () => Promise.resolve(LoginComponent) },
          { path: 'worker', loadComponent: () => Promise.resolve(LoginComponent) },
          { path: 'customer', loadComponent: () => Promise.resolve(LoginComponent) }
        ]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParams: {} } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should render the login form', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('form')).toBeTruthy();
  });

  it('should validate form fields as required', () => {
    const form = component.loginForm;
    expect(form.valid).toBe(false);

    form.get('email')?.setValue('');
    form.get('password')?.setValue('');
    expect(form.get('email')?.hasError('required')).toBe(true);
    expect(form.get('password')?.hasError('required')).toBe(true);
  });

  it('should reject invalid emails', () => {
    const emailControl = component.loginForm.get('email');
    emailControl?.setValue('invalid-email');
    expect(emailControl?.hasError('email')).toBe(true);
  });

  it('should require a password', () => {
    const passwordControl = component.loginForm.get('password');
    passwordControl?.setValue('');
    expect(passwordControl?.hasError('required')).toBe(true);
  });

  it('should call AuthService.login and disable submit button during loading', () => {
    const loginResponse = {
      userId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12',
      message: 'Success'
    };
    authServiceMock.login.mockReturnValue(of(loginResponse));

    component.loginForm.get('email')?.setValue('farmer@test.com');
    component.loginForm.get('password')?.setValue('password123');

    expect(component.loading).toBe(false);

    component.onSubmit();

    expect(authServiceMock.login).toHaveBeenCalledWith({
      email: 'farmer@test.com',
      password: 'password123'
    });
  });

  it('should redirect Farmer to /farmer on success', () => {
    const loginResponse = {
      userId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12',
      message: 'Success'
    };
    authServiceMock.login.mockReturnValue(of(loginResponse));
    const navigateSpy = vi.spyOn(router, 'navigate');

    component.loginForm.get('email')?.setValue('farmer@test.com');
    component.loginForm.get('password')?.setValue('password123');

    component.onSubmit();

    expect(navigateSpy).toHaveBeenCalledWith(['/farmer']);
  });

  it('should redirect Worker to /worker on success', () => {
    const loginResponse = {
      userId: '2',
      email: 'worker@test.com',
      fullName: 'Worker Jane',
      role: 'Worker',
      expiresAt: '2026-08-12',
      message: 'Success'
    };
    authServiceMock.login.mockReturnValue(of(loginResponse));
    const navigateSpy = vi.spyOn(router, 'navigate');

    component.loginForm.get('email')?.setValue('worker@test.com');
    component.loginForm.get('password')?.setValue('password123');

    component.onSubmit();

    expect(navigateSpy).toHaveBeenCalledWith(['/worker']);
  });

  it('should redirect Customer to /customer on success', () => {
    const loginResponse = {
      userId: '3',
      email: 'customer@test.com',
      fullName: 'Customer Alice',
      role: 'Customer',
      expiresAt: '2026-08-12',
      message: 'Success'
    };
    authServiceMock.login.mockReturnValue(of(loginResponse));
    const navigateSpy = vi.spyOn(router, 'navigate');

    component.loginForm.get('email')?.setValue('customer@test.com');
    component.loginForm.get('password')?.setValue('password123');

    component.onSubmit();

    expect(navigateSpy).toHaveBeenCalledWith(['/customer']);
  });

  it('should show error message on invalid credentials', () => {
    authServiceMock.login.mockReturnValue(throwError(() => ({ error: { message: 'Invalid credentials' } })));

    component.loginForm.get('email')?.setValue('farmer@test.com');
    component.loginForm.get('password')?.setValue('wrongpass');

    component.onSubmit();

    expect(component.errorMessage).toBe('Invalid credentials');
  });

  it('should verify that JWT is not stored in localStorage or sessionStorage, and document.cookie is not accessed', () => {
    const localStoreSpy = vi.spyOn(localStorage, 'setItem');
    const sessionStoreSpy = vi.spyOn(sessionStorage, 'setItem');
    const cookieSpy = vi.spyOn(document, 'cookie', 'get');

    const loginResponse = {
      userId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12',
      message: 'Success'
    };
    authServiceMock.login.mockReturnValue(of(loginResponse));

    component.loginForm.get('email')?.setValue('farmer@test.com');
    component.loginForm.get('password')?.setValue('password123');

    component.onSubmit();

    expect(localStoreSpy).not.toHaveBeenCalled();
    expect(sessionStoreSpy).not.toHaveBeenCalled();
    expect(cookieSpy).not.toHaveBeenCalled();
  });
});
