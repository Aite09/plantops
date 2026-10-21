import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth-guards';

// Everything is lazy-loaded (loadComponent), including the shell: the first download is only routing and guards,
// then the login page or the shell and the page being opened.
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · PlantOps',
    loadComponent: () => import('./features/login/login-page').then((m) => m.LoginPage),
  },
  {
    path: '',
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'work-orders' },
      {
        path: 'work-orders',
        title: 'Work orders · PlantOps',
        loadComponent: () =>
          import('./features/work-orders/list/work-order-list-page').then((m) => m.WorkOrderListPage),
      },
      {
        path: 'work-orders/new',
        title: 'Report a fault · PlantOps',
        loadComponent: () =>
          import('./features/work-orders/form/create-work-order-page').then((m) => m.CreateWorkOrderPage),
      },
      {
        path: 'work-orders/:id',
        title: 'Work order · PlantOps',
        loadComponent: () =>
          import('./features/work-orders/detail/work-order-detail-page').then((m) => m.WorkOrderDetailPage),
      },
      {
        path: 'work-orders/:id/edit',
        title: 'Edit work order · PlantOps',
        loadComponent: () =>
          import('./features/work-orders/form/edit-work-order-page').then((m) => m.EditWorkOrderPage),
      },
      {
        path: 'dashboard',
        title: 'Dashboard · PlantOps',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
      },
      {
        path: 'admin/users',
        title: 'Users · PlantOps',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/users/users-page').then((m) => m.UsersPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
