import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const apiUrl = environment.apiUrl;

  // Determine if the request targets the FarmKart backend API
  // Matches configured absolute base URL or typical relative API endpoints
  const isFarmKartRequest =
    req.url.startsWith(apiUrl) ||
    req.url.startsWith('/api') ||
    req.url.startsWith('api/');

  if (isFarmKartRequest) {
    const clonedRequest = req.clone({
      withCredentials: true
    });
    return next(clonedRequest);
  }

  return next(req);
};
