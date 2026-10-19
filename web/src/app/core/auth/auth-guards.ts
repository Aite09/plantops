import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from './auth.models';
import { AuthStore } from './auth-store';

// Guards decide what the user can navigate to. They are a convenience, not security: anyone can call the API
// directly, so the API enforces every permission itself.

/** Signed-in users only; everyone else goes to the login page and returns here afterwards. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthStore).isLoggedIn() ||
  inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

/** Users with at least one of the given roles; others go back to the work-order list. */
export const roleGuard =
  (...roles: Role[]): CanActivateFn =>
  () =>
    inject(AuthStore).hasAnyRole(roles) || inject(Router).createUrlTree(['/work-orders']);

/** The login page is only for signed-out users. */
export const guestGuard: CanActivateFn = () =>
  !inject(AuthStore).isLoggedIn() || inject(Router).createUrlTree(['/work-orders']);
