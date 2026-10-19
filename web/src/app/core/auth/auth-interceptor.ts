import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api-config';
import { AuthStore } from './auth-store';

/**
 * Adds "Authorization: Bearer <token>" to requests for the PlantOps API, and only those: a token sent to any
 * other host (a font CDN, an analytics script) would leak the user's identity. A 401 from the API means the token
 * expired or was rejected, so the user is signed out and sent to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  const apiBaseUrl = inject(API_BASE_URL);

  if (!isApiRequest(request.url, apiBaseUrl)) {
    return next(request);
  }

  const token = auth.token();
  const authorized = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      const isLogin = request.url.endsWith('/api/auth/login');
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLogin) {
        auth.handleUnauthorized(router.url);
      }
      return throwError(() => error);
    }),
  );
};

function isApiRequest(url: string, apiBaseUrl: string): boolean {
  // Compare against "<base>/" so http://localhost:5297 never matches http://localhost:52970.
  return apiBaseUrl ? url.startsWith(`${apiBaseUrl}/`) : url.startsWith('/api/');
}
