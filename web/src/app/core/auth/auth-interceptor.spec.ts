import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { API_BASE_URL } from '../api-config';
import { authInterceptor } from './auth-interceptor';
import { AuthStore } from './auth-store';

describe('authInterceptor', () => {
  const API = 'http://localhost:5297';

  let token: string | null;
  let auth: Pick<AuthStore, 'token' | 'handleUnauthorized'>;
  let http: HttpClient;
  let httpTesting: HttpTestingController;

  function configure(apiBaseUrl: string): void {
    token = 'test-token';
    auth = { token: () => token, handleUnauthorized: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: auth },
        { provide: API_BASE_URL, useValue: apiBaseUrl },
        // The interceptor only reads router.url, to send the user back to the current page after signing in again.
        { provide: Router, useValue: { url: '/work-orders/42' } },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpTesting.verify());

  describe('with the API on its own host', () => {
    beforeEach(() => configure(API));

    it('adds the bearer token to requests for the API', () => {
      http.get(`${API}/api/work-orders`).subscribe();

      const request = httpTesting.expectOne(`${API}/api/work-orders`);
      expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
      request.flush([]);
    });

    it('sends API requests without an Authorization header when signed out', () => {
      token = null;

      http.get(`${API}/api/work-orders`).subscribe();

      const request = httpTesting.expectOne(`${API}/api/work-orders`);
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush([]);
    });

    it('does not send the token to other hosts', () => {
      http.get('https://fonts.example.com/css2?family=Inter').subscribe();

      const request = httpTesting.expectOne('https://fonts.example.com/css2?family=Inter');
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush('');
    });

    it('does not send the token to a lookalike host whose URL starts with the API base URL', () => {
      http.get('http://localhost:52970/api/work-orders').subscribe();

      const request = httpTesting.expectOne('http://localhost:52970/api/work-orders');
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush([]);
    });

    it('signs the user out on a 401 from the API and still passes the error to the caller', () => {
      let received: HttpErrorResponse | undefined;
      http.get(`${API}/api/work-orders`).subscribe({ error: (error: HttpErrorResponse) => (received = error) });

      httpTesting
        .expectOne(`${API}/api/work-orders`)
        .flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });

      expect(auth.handleUnauthorized).toHaveBeenCalledExactlyOnceWith('/work-orders/42');
      expect(received?.status).toBe(401);
    });

    it('does not sign the user out on a 401 from the login endpoint, which only means wrong credentials', () => {
      let received: HttpErrorResponse | undefined;
      http
        .post(`${API}/api/auth/login`, { email: 'staff@plantops.test', password: 'wrong' })
        .subscribe({ error: (error: HttpErrorResponse) => (received = error) });

      httpTesting
        .expectOne(`${API}/api/auth/login`)
        .flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });

      expect(auth.handleUnauthorized).not.toHaveBeenCalled();
      expect(received?.status).toBe(401);
    });

    it('does not sign the user out on a 403, which means the action is not allowed, not that the token is bad', () => {
      http.post(`${API}/api/work-orders/42/approve`, {}).subscribe({ error: () => undefined });

      httpTesting
        .expectOne(`${API}/api/work-orders/42/approve`)
        .flush({ title: 'Forbidden', status: 403 }, { status: 403, statusText: 'Forbidden' });

      expect(auth.handleUnauthorized).not.toHaveBeenCalled();
    });
  });

  // The production environment leaves the base URL empty, so the app calls the API on its own origin.
  describe('with the API on the same origin as the app', () => {
    beforeEach(() => configure(''));

    it('adds the bearer token to relative /api/ requests', () => {
      http.get('/api/work-orders').subscribe();

      const request = httpTesting.expectOne('/api/work-orders');
      expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
      request.flush([]);
    });
  });
});
