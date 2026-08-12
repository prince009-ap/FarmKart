import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';
import { of } from 'rxjs';
import { vi } from 'vitest';

describe('authGuard', () => {
  let authServiceMock: any;
  let routerMock: any;

  beforeEach(() => {
    authServiceMock = {
      currentUserValue: null,
      checkAuthSession: vi.fn()
    };

    routerMock = {
      createUrlTree: vi.fn().mockImplementation((commands, extras) => ({ commands, extras }))
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        { provide: Router, useValue: routerMock }
      ]
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const runGuard = (route: any, state: any) => {
    return TestBed.runInInjectionContext(() => authGuard(route, state));
  };

  const resolveHelper = async (res: any) => {
    if (res && typeof res.subscribe === 'function') {
      return new Promise(resolve => res.subscribe(resolve));
    }
    return res;
  };

  it('should allow access for authenticated user', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ userId: '1', email: 'test@test.com', role: 'Farmer' }));

    const result = await resolveHelper(runGuard({} as any, { url: '/protected' } as any));
    expect(result).toBe(true);
  });

  it('should redirect unauthenticated user to login and preserve original URL in returnUrl query parameter', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of(null));

    const result: any = await resolveHelper(runGuard({} as any, { url: '/farmer' } as any));
    expect(result).toBeTruthy();
    expect(result.commands).toEqual(['/login']);
    expect(result.extras?.queryParams).toEqual({ returnUrl: '/farmer' });
  });

  it('should not read JWT, access document.cookie, or localStorage/sessionStorage', async () => {
    const cookieSpy = vi.spyOn(document, 'cookie', 'get');
    const localStoreSpy = vi.spyOn(localStorage, 'getItem');
    const sessionStoreSpy = vi.spyOn(sessionStorage, 'getItem');

    authServiceMock.checkAuthSession.mockReturnValue(of({ userId: '1', role: 'Farmer' }));

    await resolveHelper(runGuard({} as any, { url: '/farmer' } as any));

    expect(cookieSpy).not.toHaveBeenCalled();
    expect(localStoreSpy).not.toHaveBeenCalled();
    expect(sessionStoreSpy).not.toHaveBeenCalled();
  });
});
