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
        path: 'auctions',
        loadComponent: () =>
          import('./features/farmer/farmer-auctions.component').then(
            (module) => module.FarmerAuctionsComponent,
          ),
      },
      {
        path: 'auctions/:id',
        loadComponent: () =>
          import('./features/farmer/farmer-auction-detail.component').then(
            (module) => module.FarmerAuctionDetailComponent,
          ),
      },
      {
        path: 'auctions/:id/bids',
        loadComponent: () =>
          import('./features/farmer/farmer-auction-bids.component').then(
            (module) => module.FarmerAuctionBidsComponent,
          ),
      },
      {
        path: 'orders',
        loadComponent: () =>
          import('./features/farmer/farmer-orders.component').then(
            (module) => module.FarmerOrdersComponent,
          ),
      },
      {
        path: 'orders/:id',
        loadComponent: () =>
          import('./features/farmer/farmer-order-detail.component').then(
            (module) => module.FarmerOrderDetailComponent,
          ),
      },
      {
        path: 'orders/:id/invoice',
        loadComponent: () =>
          import('./features/farmer/farmer-invoice.component').then(
            (module) => module.FarmerInvoiceComponent,
          ),
      },
      {
        path: 'reviews',
        loadComponent: () =>
          import('./features/farmer/farmer-reviews.component').then(
            (module) => module.FarmerReviewsComponent,
          ),
      },
      {
        path: 'machinery',
        loadComponent: () =>
          import('./features/farmer/my-machinery.component').then(
            (module) => module.MyMachineryComponent,
          ),
      },
      {
        path: 'machinery/new',
        loadComponent: () =>
          import('./features/farmer/my-machinery-form.component').then(
            (module) => module.MyMachineryFormComponent,
          ),
      },
      {
        path: 'machinery/rentals',
        loadComponent: () =>
          import('./features/farmer/my-machinery-rentals.component').then(
            (module) => module.MyMachineryRentalsComponent,
          ),
      },
      {
        path: 'machinery/:id/edit',
        loadComponent: () =>
          import('./features/farmer/my-machinery-form.component').then(
            (module) => module.MyMachineryFormComponent,
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
        loadComponent: () =>
          import('./features/farmer/farmer-notifications.component').then(
            (module) => module.FarmerNotificationsComponent,
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
      import('./features/customer/customer-shell.component').then(
        (module) => module.CustomerShellComponent,
      ),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/customer/customer-dashboard.component').then(
            (module) => module.CustomerDashboardComponent,
          ),
      },
      {
        path: 'auctions',
        loadComponent: () =>
          import('./features/customer/customer-auctions.component').then(
            (module) => module.CustomerAuctionsComponent,
          ),
      },
      {
        path: 'auctions/:id',
        loadComponent: () =>
          import('./features/customer/customer-auction-detail.component').then(
            (module) => module.CustomerAuctionDetailComponent,
          ),
      },
      {
        path: 'auctions/:id/bids',
        loadComponent: () =>
          import('./features/customer/customer-auction-bids.component').then(
            (module) => module.CustomerAuctionBidsComponent,
          ),
      },
      {
        path: 'auctions/:id/checkout',
        loadComponent: () =>
          import('./features/customer/customer-checkout.component').then(
            (module) => module.CustomerCheckoutComponent,
          ),
      },
      {
        path: 'bids',
        loadComponent: () =>
          import('./features/customer/customer-bids.component').then(
            (module) => module.CustomerBidsComponent,
          ),
      },
      {
        path: 'payments',
        loadComponent: () =>
          import('./features/customer/customer-payments.component').then(
            (module) => module.CustomerPaymentsComponent,
          ),
      },
      {
        path: 'orders',
        loadComponent: () =>
          import('./features/customer/customer-orders.component').then(
            (module) => module.CustomerOrdersComponent,
          ),
      },
      {
        path: 'orders/:id',
        loadComponent: () =>
          import('./features/customer/customer-order-detail.component').then(
            (module) => module.CustomerOrderDetailComponent,
          ),
      },
      {
        path: 'orders/:id/track',
        loadComponent: () =>
          import('./features/customer/customer-order-tracking.component').then(
            (module) => module.CustomerOrderTrackingComponent,
          ),
      },
      {
        path: 'orders/:id/invoice',
        loadComponent: () =>
          import('./features/customer/customer-invoice.component').then(
            (module) => module.CustomerInvoiceComponent,
          ),
      },
      {
        path: 'wishlist',
        loadComponent: () =>
          import('./features/customer/customer-wishlist.component').then(
            (module) => module.CustomerWishlistComponent,
          ),
      },
      {
        path: 'reviews',
        loadComponent: () =>
          import('./features/customer/customer-reviews.component').then(
            (module) => module.CustomerReviewsComponent,
          ),
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/customer/customer-notifications.component').then(
            (module) => module.CustomerNotificationsComponent,
          ),
      },
      {
        path: 'machinery',
        loadComponent: () =>
          import('./features/customer/customer-machinery.component').then(
            (module) => module.CustomerMachineryComponent,
          ),
      },
      {
        path: 'machinery/:id',
        loadComponent: () =>
          import('./features/customer/customer-machinery-detail.component').then(
            (module) => module.CustomerMachineryDetailComponent,
          ),
      },
      {
        path: 'my-rentals',
        loadComponent: () =>
          import('./features/customer/customer-my-rentals.component').then(
            (module) => module.CustomerMyRentalsComponent,
          ),
      },
      {
        path: 'profile',
        data: { title: 'My Profile' },
        loadComponent: () =>
          import('./features/customer/coming-soon.component').then(
            (module) => module.ComingSoonComponent,
          ),
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
