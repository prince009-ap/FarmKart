import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { roleGuard } from './role.guard';
import { AuthService } from '../services/auth.service';
import { of } from 'rxjs';
import { vi } from 'vitest';

describe('roleGuard', () => {
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
    return TestBed.runInInjectionContext(() => roleGuard(route, state));
  };

  const resolveHelper = async (res: any) => {
    if (res && typeof res.subscribe === 'function') {
      return new Promise(resolve => res.subscribe(resolve));
    }
    return res;
  };

  it('should allow access if user has required role (Farmer on Farmer route)', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Farmer' }));
    const route = { data: { roles: ['Farmer'] } };

    const result = await resolveHelper(runGuard(route as any, {} as any));
    expect(result).toBe(true);
  });

  it('should block and redirect Worker attempting to access Farmer route', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Worker' }));
    const route = { data: { roles: ['Farmer'] } };

    const result: any = await resolveHelper(runGuard(route as any, {} as any));
    expect(result).toBeTruthy();
    expect(result.commands).toEqual(['/unauthorized']);
  });

  it('should block and redirect Customer attempting to access Farmer route', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Customer' }));
    const route = { data: { roles: ['Farmer'] } };

    const result: any = await resolveHelper(runGuard(route as any, {} as any));
    expect(result).toBeTruthy();
    expect(result.commands).toEqual(['/unauthorized']);
  });

  it('should allow Worker to access Worker route', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Worker' }));
    const route = { data: { roles: ['Worker'] } };

    const result = await resolveHelper(runGuard(route as any, {} as any));
    expect(result).toBe(true);
  });

  it('should allow Customer to access Customer route', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Customer' }));
    const route = { data: { roles: ['Customer'] } };

    const result = await resolveHelper(runGuard(route as any, {} as any));
    expect(result).toBe(true);
  });

  it('should allow access when user role is in multiple allowed roles list', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Worker' }));
    const route = { data: { roles: ['Farmer', 'Worker'] } };

    const result = await resolveHelper(runGuard(route as any, {} as any));
    expect(result).toBe(true);
  });

  it('should redirect unauthenticated users to login', async () => {
    authServiceMock.checkAuthSession.mockReturnValue(of(null));
    const route = { data: { roles: ['Farmer'] } };

    const result: any = await resolveHelper(runGuard(route as any, {} as any));
    expect(result).toBeTruthy();
    expect(result.commands).toEqual(['/login']);
  });

  it('should rely solely on safe AuthService session and not read or decode JWT', async () => {
    const cookieSpy = vi.spyOn(document, 'cookie', 'get');
    authServiceMock.checkAuthSession.mockReturnValue(of({ role: 'Farmer' }));

    const route = { data: { roles: ['Farmer'] } };
    const result = await resolveHelper(runGuard(route as any, {} as any));

    expect(result).toBe(true);
    expect(cookieSpy).not.toHaveBeenCalled();
  });
});
