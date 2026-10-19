import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../api-config';
import { AccessTokenResponse, CurrentUser, Role } from './auth.models';

interface Session {
  token: string;
  expiresAt: number;
  user: CurrentUser;
}

// sessionStorage, not localStorage: the token is per tab and survives a refresh, but it is not shared with other
// tabs or kept as a long-lived browser setting. Browsers do restore it when a closed tab is reopened (and copy it on
// "Duplicate tab"), so on a shared control-room PC people must use Sign out; the token itself stays valid at the API
// for at most 60 minutes. Any script on the page could read it, which is why the app has no third-party scripts and
// Angular escapes all rendered data (XSS is the threat to watch). The stronger option is an HttpOnly cookie set by
// a backend-for-frontend. Microsoft's MSAL library also defaults to sessionStorage.
const STORAGE_KEY = 'plantops.session';

/** Who is signed in. Components read signals; the interceptor reads the token. */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  readonly #http = inject(HttpClient);
  readonly #router = inject(Router);
  readonly #apiBaseUrl = inject(API_BASE_URL);

  readonly #session = signal<Session | null>(readStoredSession());
  #expiryTimer: ReturnType<typeof setTimeout> | undefined;

  readonly user = computed(() => this.#session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.#session() !== null);

  constructor() {
    const session = this.#session();
    if (session) {
      this.#scheduleExpiry(session);
    }
  }

  /** The bearer token, or null when signed out or expired. */
  token(): string | null {
    const session = this.#session();
    return session && session.expiresAt > Date.now() ? session.token : null;
  }

  hasAnyRole(roles: readonly Role[]): boolean {
    const user = this.user();
    return user !== null && roles.some((role) => user.roles.includes(role));
  }

  login(email: string, password: string): Observable<CurrentUser> {
    return this.#http
      .post<AccessTokenResponse>(`${this.#apiBaseUrl}/api/auth/login`, { email, password })
      .pipe(
        map((response) => {
          const session: Session = {
            token: response.accessToken,
            // Measured from this device's clock, not compared with the server's absolute expiry time.
            expiresAt: Date.now() + response.expiresInSeconds * 1000,
            user: response.user,
          };
          this.#start(session);
          return session.user;
        }),
      );
  }

  logout(): void {
    this.#end();
    void this.#router.navigate(['/login']);
  }

  /** The API rejected our token (expired or revoked): sign out and come back here after signing in again. */
  handleUnauthorized(returnUrl: string): void {
    if (!this.#session()) {
      return;
    }
    this.#end();
    void this.#router.navigate(['/login'], { queryParams: { returnUrl, reason: 'expired' } });
  }

  #start(session: Session): void {
    this.#session.set(session);
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Storage can be unavailable (privacy modes); the session then lasts until the page is reloaded.
    }
    this.#scheduleExpiry(session);
  }

  #end(): void {
    clearTimeout(this.#expiryTimer);
    this.#session.set(null);
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing to clean up if storage is unavailable.
    }
  }

  // Sign out when the token expires instead of waiting for the next request to fail.
  #scheduleExpiry(session: Session): void {
    clearTimeout(this.#expiryTimer);
    this.#expiryTimer = setTimeout(
      () => this.handleUnauthorized(this.#router.url),
      Math.max(0, session.expiresAt - Date.now()),
    );
  }
}

function readStoredSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const session = JSON.parse(raw) as Partial<Session>;
    const valid =
      typeof session.token === 'string' &&
      typeof session.expiresAt === 'number' &&
      session.expiresAt > Date.now() &&
      Array.isArray(session.user?.roles);
    return valid ? (session as Session) : null;
  } catch {
    return null;
  }
}
