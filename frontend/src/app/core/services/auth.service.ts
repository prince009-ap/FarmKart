import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  LoginRequest,
  LoginResponse,
  RegisterFarmerRequest,
  FarmerRegistrationResponse,
  RegisterWorkerRequest,
  WorkerRegistrationResponse,
  RegisterCustomerRequest,
  CustomerRegistrationResponse,
  AuthUser
} from '../models/auth.models';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  private readonly currentUserSubject = new BehaviorSubject<AuthUser | null>(null);
  public readonly currentUser$: Observable<AuthUser | null> = this.currentUserSubject.asObservable();

  registerFarmer(request: RegisterFarmerRequest): Observable<FarmerRegistrationResponse> {
    return this.http.post<FarmerRegistrationResponse>(
      `${this.apiUrl}/auth/register/farmer`,
      request
    );
  }

  registerWorker(request: RegisterWorkerRequest): Observable<WorkerRegistrationResponse> {
    return this.http.post<WorkerRegistrationResponse>(
      `${this.apiUrl}/auth/register/worker`,
      request
    );
  }

  registerCustomer(request: RegisterCustomerRequest): Observable<CustomerRegistrationResponse> {
    return this.http.post<CustomerRegistrationResponse>(
      `${this.apiUrl}/auth/register/customer`,
      request
    );
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(
      `${this.apiUrl}/auth/login`,
      request,
      { withCredentials: true }
    ).pipe(
      tap(response => {
        const user: AuthUser = {
          userId: response.userId,
          email: response.email,
          fullName: response.fullName,
          role: response.role
        };
        this.currentUserSubject.next(user);
      })
    );
  }

  logout(): void {
    // Note: Since HttpOnly cookie deletion is handled by the backend, and no backend logout endpoint exists yet,
    // we clear the local user state here. In subsequent phases, we will call a backend logout API.
    this.currentUserSubject.next(null);
  }

  getCurrentUser(): Observable<AuthUser> {
    return this.http.get<AuthUser>(
      `${this.apiUrl}/auth/current-user`,
      { withCredentials: true }
    ).pipe(
      tap(user => this.currentUserSubject.next(user))
    );
  }

  get currentUserValue(): AuthUser | null {
    return this.currentUserSubject.value;
  }
}
