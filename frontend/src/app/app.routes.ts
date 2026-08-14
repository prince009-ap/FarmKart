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
    redirectTo: 'auth/login',
    pathMatch: 'full'
  },
  {
    path: 'register',
    redirectTo: 'auth/register/customer',
    pathMatch: 'full'
  },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/login.component').then(
        (module) => module.LoginComponent,
      ),
  },
  {
    path: 'auth/register/farmer',
    loadComponent: () =>
      import('./features/auth/register-farmer.component').then(
        (module) => module.RegisterFarmerComponent,
      ),
  },
  {
    path: 'auth/register/worker',
    loadComponent: () =>
      import('./features/auth/register-worker.component').then(
        (module) => module.RegisterWorkerComponent,
      ),
  },
  {
    path: 'auth/register/customer',
    loadComponent: () =>
      import('./features/auth/register-customer.component').then(
        (module) => module.RegisterCustomerComponent,
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
      import('./features/farmer/farmer-shell.component').then(
        (module) => module.FarmerShellComponent,
      ),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/farmer/farmer-dashboard.component').then(
            (module) => module.FarmerDashboardComponent,
          ),
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/farmer/farmer-profile.component').then(
            (module) => module.FarmerProfileComponent,
          ),
      },
      {
        path: 'jobs',
        loadComponent: () =>
          import('./features/farmer/farmer-jobs.component').then(
            (module) => module.FarmerJobsComponent,
          ),
      },
      { path: 'jobs/create', loadComponent: () => import('./features/farmer/farmer-job-form.component').then((module) => module.FarmerJobFormComponent) },
      { path: 'jobs/:id', loadComponent: () => import('./features/farmer/farmer-job-detail.component').then((module) => module.FarmerJobDetailComponent) },
      { path: 'jobs/:id/edit', loadComponent: () => import('./features/farmer/farmer-job-form.component').then((module) => module.FarmerJobFormComponent) },
      { path: 'jobs/:jobId/applications', loadComponent: () => import('./features/farmer/farmer-job-applications.component').then((module) => module.FarmerJobApplicationsComponent) },
      { path: 'jobs/:jobId/assignments', loadComponent: () => import('./features/farmer/farmer-job-assignments.component').then((module) => module.FarmerJobAssignmentsComponent) },
      { path: 'jobs/:jobId/attendance', loadComponent: () => import('./features/farmer/farmer-attendance.component').then((module) => module.FarmerAttendanceComponent) },
      {
        path: 'crops',
        loadComponent: () =>
          import('./features/farmer/farmer-crops.component').then(
            (module) => module.FarmerCropsComponent,
          ),
      },
      {
        path: 'crops/new',
        loadComponent: () =>
          import('./features/farmer/farmer-crop-form.component').then(
            (module) => module.FarmerCropFormComponent,
          ),
      },
      {
        path: 'crops/:id',
        loadComponent: () =>
          import('./features/farmer/farmer-crop-detail.component').then(
            (module) => module.FarmerCropDetailComponent,
          ),
      },
      {
        path: 'crops/:id/edit',
        loadComponent: () =>
          import('./features/farmer/farmer-crop-form.component').then(
            (module) => module.FarmerCropFormComponent,
          ),
      },
      {
        path: 'machinery',
        data: { title: 'Machinery' },
        loadComponent: () =>
          import('./features/farmer/coming-soon.component').then(
            (module) => module.ComingSoonComponent,
          ),
      },
      {
        path: 'marketplace',
        data: { title: 'Marketplace' },
        loadComponent: () =>
          import('./features/farmer/coming-soon.component').then(
            (module) => module.ComingSoonComponent,
          ),
      },
      {
        path: 'notifications',
        data: { title: 'Notifications' },
        loadComponent: () =>
          import('./features/farmer/coming-soon.component').then(
            (module) => module.ComingSoonComponent,
          ),
      },
    ],
  },
  {
    path: 'worker',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Worker'] },
    loadComponent: () =>
      import('./features/worker/worker-shell.component').then(
        (module) => module.WorkerShellComponent,
      ),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/worker/worker-dashboard.component').then(
            (module) => module.WorkerDashboardComponent,
          ),
      },
      {
        path: 'jobs',
        loadComponent: () =>
          import('./features/worker/worker-jobs.component').then(
            (module) => module.WorkerJobsComponent,
          ),
      },
      {
        path: 'jobs/:id',
        loadComponent: () =>
          import('./features/worker/worker-job-detail.component').then(
            (module) => module.WorkerJobDetailComponent,
          ),
      },
      {
        path: 'applications',
        loadComponent: () =>
          import('./features/worker/worker-applications.component').then(
            (module) => module.WorkerApplicationsComponent,
          ),
      },
      {
        path: 'assignments',
        loadComponent: () =>
          import('./features/worker/worker-assignments.component').then(
            (module) => module.WorkerAssignmentsComponent,
          ),
      },
      {
        path: 'assignments/:id',
        loadComponent: () =>
          import('./features/worker/worker-assignment-detail.component').then(
            (module) => module.WorkerAssignmentDetailComponent,
          ),
      },
      {
        path: 'attendance',
        loadComponent: () =>
          import('./features/worker/worker-attendance.component').then(
            (module) => module.WorkerAttendanceComponent,
          ),
      },
      {
        path: 'assignments/:assignmentId/attendance',
        loadComponent: () =>
          import('./features/worker/worker-attendance.component').then(
            (module) => module.WorkerAttendanceComponent,
          ),
      },
      {
        path: 'earnings',
        loadComponent: () =>
          import('./features/worker/worker-earnings.component').then(
            (module) => module.WorkerEarningsComponent,
          ),
      },
      {
        path: 'work-history',
        loadComponent: () =>
          import('./features/worker/worker-work-history.component').then(
            (module) => module.WorkerWorkHistoryComponent,
          ),
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/worker/worker-profile.component').then(
            (module) => module.WorkerProfileComponent,
          ),
      },
      {
        path: 'preferences',
        loadComponent: () =>
          import('./features/worker/worker-preferences.component').then(
            (module) => module.WorkerPreferencesComponent,
          ),
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/worker/worker-notifications.component').then(
            (module) => module.WorkerNotificationsComponent,
          ),
      },
    ],
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
