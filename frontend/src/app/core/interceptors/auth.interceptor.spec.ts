import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { environment } from '../../../environments/environment';
import { vi } from 'vitest';

describe('authInterceptor', () => {
  let httpMock: HttpTestingController;
  let httpClient: HttpClient;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpMock = TestBed.inject(HttpTestingController);
    httpClient = TestBed.inject(HttpClient);
  });

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  it('should set withCredentials = true for requests targeting FarmKart backend API', () => {
    httpClient.get(`${environment.apiUrl}/test-endpoint`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/test-endpoint`);
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('should set withCredentials = true for relative backend API URLs starting with /api', () => {
    httpClient.get('/api/test-endpoint').subscribe();

    const req = httpMock.expectOne('/api/test-endpoint');
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('should set withCredentials = true for relative backend API URLs starting with api/', () => {
    httpClient.get('api/test-endpoint').subscribe();

    const req = httpMock.expectOne('api/test-endpoint');
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('should not set withCredentials = true for external/non-FarmKart requests', () => {
    httpClient.get('https://api.github.com/users').subscribe();

    const req = httpMock.expectOne('https://api.github.com/users');
    expect(req.request.withCredentials).toBe(false);
    req.flush({});
  });

  it('should preserve original request properties (headers, body, method)', () => {
    const headers = { 'X-Custom-Header': 'CustomValue' };
    const body = { data: 'test' };

    httpClient.post(`${environment.apiUrl}/test`, body, { headers }).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/test`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-Custom-Header')).toBe('CustomValue');
    expect(req.request.body).toEqual(body);
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('should not add an Authorization header', () => {
    httpClient.get(`${environment.apiUrl}/test`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/test`);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should not access document.cookie, localStorage, or sessionStorage', () => {
    const cookieSpy = vi.spyOn(document, 'cookie', 'get');
    const localStoreSpy = vi.spyOn(localStorage, 'setItem');
    const sessionStoreSpy = vi.spyOn(sessionStorage, 'setItem');

    httpClient.get(`${environment.apiUrl}/test`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/test`);
    req.flush({});

    expect(cookieSpy).not.toHaveBeenCalled();
    expect(localStoreSpy).not.toHaveBeenCalled();
    expect(sessionStoreSpy).not.toHaveBeenCalled();
  });
});
