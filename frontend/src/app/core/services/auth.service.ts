import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { tap, catchError, shareReplay } from 'rxjs/operators';
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

  private hasCheckedSession = false;
  private sessionCheck$: Observable<AuthUser | null> | null = null;

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
          role: response.role,
          profileImageUrl: response.profileImageUrl
        };
        this.currentUserSubject.next(user);
        this.hasCheckedSession = true;
      })
    );
  }

  logout(): void {
    // Note: Since HttpOnly cookie deletion is handled by the backend, and no backend logout endpoint exists yet,
    // we clear the local user state here. In subsequent phases, we will call a backend logout API.
    this.currentUserSubject.next(null);
    this.hasCheckedSession = false;
    this.sessionCheck$ = null;
  }

  getCurrentUser(): Observable<AuthUser> {
    return this.http.get<AuthUser>(
      `${this.apiUrl}/auth/current-user`,
      { withCredentials: true }
    ).pipe(
      tap(user => this.currentUserSubject.next(user))
    );
  }

  updateUserProfileImage(profileImageUrl: string | null): void {
    const current = this.currentUserSubject.value;
    if (current) {
      const updated = { ...current, profileImageUrl };
      this.currentUserSubject.next(updated);
    }
  }

  checkAuthSession(): Observable<AuthUser | null> {
    if (this.hasCheckedSession) {
      return of(this.currentUserSubject.value);
    }

    if (this.sessionCheck$) {
      return this.sessionCheck$;
    }

    this.sessionCheck$ = this.getCurrentUser().pipe(
      tap(() => {
        this.hasCheckedSession = true;
        this.sessionCheck$ = null;
      }),
      catchError(() => {
        this.hasCheckedSession = true;
        this.currentUserSubject.next(null);
        this.sessionCheck$ = null;
        return of(null);
      }),
      shareReplay(1)
    );

    return this.sessionCheck$;
  }

  get currentUserValue(): AuthUser | null {
    return this.currentUserSubject.value;
  }
}
