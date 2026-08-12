import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Location } from '@angular/common';
import { routes } from './app.routes';
import { AuthService } from './core/services/auth.service';
import { of } from 'rxjs';
import { vi } from 'vitest';

describe('App Routes', () => {
  let router: Router;
  let location: Location;
  let authServiceMock: any;

  beforeEach(async () => {
    authServiceMock = {
      currentUserValue: null,
      checkAuthSession: vi.fn().mockReturnValue(of(null))
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: AuthService, useValue: authServiceMock }
      ]
    });

    router = TestBed.inject(Router);
    location = TestBed.inject(Location);
    
    router.initialNavigation();
  });

  it('should redirect unauthenticated users from /farmer to /login', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of(null));
    
    await router.navigate(['/farmer']);
    
    expect(location.path()).toBe('/login?returnUrl=%2Ffarmer');
  });

  it('should redirect unauthenticated users from /worker to /login', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of(null));
    
    await router.navigate(['/worker']);
    
    expect(location.path()).toBe('/login?returnUrl=%2Fworker');
  });

  it('should redirect unauthenticated users from /customer to /login', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of(null));
    
    await router.navigate(['/customer']);
    
    expect(location.path()).toBe('/login?returnUrl=%2Fcustomer');
  });

  it('should allow access to public /login route', async () => {
    await router.navigate(['/login']);
    
    expect(location.path()).toBe('/login');
  });

  it('should allow access to public /register route', async () => {
    await router.navigate(['/register']);
    
    expect(location.path()).toBe('/register');
  });

  it('should allow access to public /unauthorized route', async () => {
    await router.navigate(['/unauthorized']);
    
    expect(location.path()).toBe('/unauthorized');
  });

  it('should allow Farmer to access /farmer', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Farmer' }));
    
    await router.navigate(['/farmer']);
    
    expect(location.path()).toBe('/farmer');
  });

  it('should redirect Farmer attempting /worker to /unauthorized', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Farmer' }));
    
    await router.navigate(['/worker']);
    
    expect(location.path()).toBe('/unauthorized');
  });
});
