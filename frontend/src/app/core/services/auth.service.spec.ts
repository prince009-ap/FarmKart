import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';
import {
  LoginRequest,
  RegisterFarmerRequest,
  RegisterWorkerRequest,
  RegisterCustomerRequest
} from '../models/auth.models';
import { vi } from 'vitest';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call login and use withCredentials: true', () => {
    const mockRequest: LoginRequest = { email: 'farmer@test.com', password: 'password123' };
    const mockResponse = {
      userId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12T23:25:37+05:30',
      message: 'Login successful'
    };

    service.login(mockRequest).subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.body).toEqual(mockRequest);

    req.flush(mockResponse);
  });

  it('should call registerFarmer with correct endpoint and payload', () => {
    const mockRequest: RegisterFarmerRequest = {
      fullName: 'Farmer John',
      email: 'farmer@test.com',
      password: 'password123',
      phone: '1234567890',
      profileImageUrl: null,
      address: '123 Farm Road',
      farmName: 'Happy Farm',
      farmSize: 10.5,
      farmLocation: null
    };

    const mockResponse = {
      farmerId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      message: 'Registration successful'
    };

    service.registerFarmer(mockRequest).subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/register/farmer`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(mockRequest);

    req.flush(mockResponse);
  });

  it('should call registerWorker with correct endpoint and payload', () => {
    const mockRequest: RegisterWorkerRequest = {
      fullName: 'Worker Jane',
      email: 'worker@test.com',
      password: 'password123',
      phone: '0987654321',
      profileImageUrl: null,
      address: '456 Field Lane',
      experienceYears: 5,
      expectedDailyWage: 150
    };

    const mockResponse = {
      workerId: '2',
      email: 'worker@test.com',
      fullName: 'Worker Jane',
      message: 'Registration successful'
    };

    service.registerWorker(mockRequest).subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/register/worker`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(mockRequest);

    req.flush(mockResponse);
  });

  it('should call registerCustomer with correct endpoint and payload', () => {
    const mockRequest: RegisterCustomerRequest = {
      fullName: 'Customer Alice',
      email: 'customer@test.com',
      password: 'password123',
      phone: '1112223333',
      profileImageUrl: null,
      address: '789 Main Street'
    };

    const mockResponse = {
      customerId: '3',
      email: 'customer@test.com',
      fullName: 'Customer Alice',
      message: 'Registration successful'
    };

    service.registerCustomer(mockRequest).subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/register/customer`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(mockRequest);

    req.flush(mockResponse);
  });

  it('should not store JWT in localStorage or sessionStorage', () => {
    const localStoreSpy = vi.spyOn(localStorage, 'setItem');
    const sessionStoreSpy = vi.spyOn(sessionStorage, 'setItem');

    const mockRequest: LoginRequest = { email: 'farmer@test.com', password: 'password123' };
    const mockResponse = {
      userId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12T23:25:37+05:30',
      message: 'Login successful'
    };

    service.login(mockRequest).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush(mockResponse);

    expect(localStoreSpy).not.toHaveBeenCalled();
    expect(sessionStoreSpy).not.toHaveBeenCalled();
  });

  it('should never access document.cookie in Angular code', () => {
    const cookieSpy = vi.spyOn(document, 'cookie', 'get');

    const mockRequest: LoginRequest = { email: 'farmer@test.com', password: 'password123' };
    const mockResponse = {
      userId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12T23:25:37+05:30',
      message: 'Login successful'
    };

    service.login(mockRequest).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush(mockResponse);

    expect(cookieSpy).not.toHaveBeenCalled();
  });

  it('should update safe current-user state on successful login', () => {
    const mockRequest: LoginRequest = { email: 'farmer@test.com', password: 'password123' };
    const mockResponse = {
      userId: '123e4567-e89b-12d3-a456-426614174000',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12T23:25:37+05:30',
      message: 'Login successful'
    };

    let currentUser: any = null;
    service.currentUser$.subscribe(user => {
      currentUser = user;
    });

    service.login(mockRequest).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush(mockResponse);

    expect(currentUser).toBeTruthy();
    expect(currentUser.userId).toBe(mockResponse.userId);
    expect(currentUser.email).toBe(mockResponse.email);
    expect(currentUser.fullName).toBe(mockResponse.fullName);
    expect(currentUser.role).toBe(mockResponse.role);
    expect(service.currentUserValue).toEqual(currentUser);
  });

  it('should not authenticate user on failed login', () => {
    const mockRequest: LoginRequest = { email: 'farmer@test.com', password: 'wrongpassword' };

    let currentUser: any = null;
    service.currentUser$.subscribe(user => {
      currentUser = user;
    });

    service.login(mockRequest).subscribe({
      error: () => {}
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(currentUser).toBeNull();
    expect(service.currentUserValue).toBeNull();
  });

  it('should clear local authentication state on logout', () => {
    // 1. First populate state with login
    const mockResponse = {
      userId: '1',
      email: 'farmer@test.com',
      fullName: 'Farmer John',
      role: 'Farmer',
      expiresAt: '2026-08-12T23:25:37+05:30',
      message: 'Login successful'
    };

    service.login({ email: 'farmer@test.com', password: 'password' }).subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush(mockResponse);
    expect(service.currentUserValue).toBeTruthy();

    // 2. Logout
    service.logout();

    // 3. Assert state is cleared
    expect(service.currentUserValue).toBeNull();
  });
});
