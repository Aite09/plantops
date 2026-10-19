import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api-config';
import { CurrentUser, Role } from '../../core/auth/auth.models';

export type UserSummary = CurrentUser;

export interface NewUser {
  email: string;
  displayName: string;
  password: string;
  role: Role;
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  readonly #http = inject(HttpClient);
  readonly #base = `${inject(API_BASE_URL)}/api/users`;

  list(role?: Role): Observable<UserSummary[]> {
    const params = role ? new HttpParams().set('role', role) : undefined;
    return this.#http.get<UserSummary[]>(this.#base, { params });
  }

  create(user: NewUser): Observable<{ id: string }> {
    return this.#http.post<{ id: string }>(this.#base, user);
  }
}
