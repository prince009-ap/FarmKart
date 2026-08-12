import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { map } from 'rxjs/operators';

export const roleGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const allowedRoles = route.data?.['roles'] as string[] | undefined;

  return authService.checkAuthSession().pipe(
    map(user => {
      if (!user) {
        return router.createUrlTree(['/login']);
      }

      if (!allowedRoles || allowedRoles.includes(user.role)) {
        return true;
      }

      return router.createUrlTree(['/unauthorized']);
    })
  );
};
