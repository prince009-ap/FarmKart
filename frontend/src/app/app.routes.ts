import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./layouts/app-shell/app-shell.component').then(
        (module) => module.AppShellComponent,
      ),
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.component').then(
        (module) => module.LoginComponent,
      ),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register.component').then(
        (module) => module.RegisterComponent,
      ),
  },
  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./features/auth/unauthorized.component').then(
        (module) => module.UnauthorizedComponent,
      ),
  },
  {
    path: 'farmer',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Farmer'] },
    loadComponent: () =>
      import('./features/farmer/farmer-dashboard.component').then(
        (module) => module.FarmerDashboardComponent,
      ),
  },
  {
    path: 'worker',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Worker'] },
    loadComponent: () =>
      import('./features/worker/worker-dashboard.component').then(
        (module) => module.WorkerDashboardComponent,
      ),
  },
  {
    path: 'customer',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Customer'] },
    loadComponent: () =>
      import('./features/customer/customer-dashboard.component').then(
        (module) => module.CustomerDashboardComponent,
      ),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
