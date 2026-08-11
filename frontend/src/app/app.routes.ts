import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./layouts/app-shell/app-shell.component').then(
        (module) => module.AppShellComponent,
      ),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
