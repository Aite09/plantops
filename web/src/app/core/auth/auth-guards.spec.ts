import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  GuardResult,
  MaybeAsync,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { Role } from './auth.models';
import { authGuard, guestGuard, roleGuard } from './auth-guards';
import { AuthStore } from './auth-store';

/** Stands in for AuthStore: the guards only read isLoggedIn() and hasAnyRole(). */
class FakeAuthStore {
  readonly isLoggedIn = signal(false);
  roles: Role[] = [];

  hasAnyRole(roles: readonly Role[]): boolean {
    return roles.some((role) => this.roles.includes(role));
  }
}

describe('auth guards', () => {
  let auth: FakeAuthStore;

  beforeEach(() => {
    auth = new FakeAuthStore();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthStore, useValue: auth }],
    });
  });

  function signIn(...roles: Role[]): void {
    auth.isLoggedIn.set(true);
    auth.roles = roles;
  }

  function run(guard: CanActivateFn, url = '/work-orders'): MaybeAsync<GuardResult> {
    return TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );
  }

  function redirectTarget(result: MaybeAsync<GuardResult>): string {
    expect(result).toBeInstanceOf(UrlTree);
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }

  describe('authGuard', () => {
    it('lets a signed-in user through', () => {
      signIn('Staff');

      expect(run(authGuard)).toBe(true);
    });

    it('sends a signed-out user to the login page with a returnUrl back to the requested page', () => {
      const result = run(authGuard, '/work-orders/42');

      expect(redirectTarget(result)).toBe('/login?returnUrl=%2Fwork-orders%2F42');
    });
  });

  describe('roleGuard', () => {
    it('lets a user with the required role through', () => {
      signIn('Admin');

      expect(run(roleGuard('Admin'), '/admin/users')).toBe(true);
    });

    it('lets a user through who has any one of the listed roles', () => {
      // Admin is the second listed role, so a guard that only checked the first one would redirect here.
      signIn('Admin');

      expect(run(roleGuard('Supervisor', 'Admin'))).toBe(true);
    });

    it('sends a user without any of the roles back to the work-order list', () => {
      signIn('Supervisor', 'Technician');

      const result = run(roleGuard('Admin'), '/admin/users');

      expect(redirectTarget(result)).toBe('/work-orders');
    });
  });

  describe('guestGuard', () => {
    it('lets a signed-out user open the login page', () => {
      expect(run(guestGuard, '/login')).toBe(true);
    });

    it('sends a signed-in user from the login page to the work-order list', () => {
      signIn('Staff');

      const result = run(guestGuard, '/login');

      expect(redirectTarget(result)).toBe('/work-orders');
    });
  });
});
