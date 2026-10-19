export type Role = 'Staff' | 'Supervisor' | 'Technician' | 'Admin';

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  roles: Role[];
}

/** Response of POST /api/auth/login. */
export interface AccessTokenResponse {
  accessToken: string;
  expiresAtUtc: string;
  /** Lifetime in seconds, so expiry is timed on this device's clock (a fast PC clock can't end it early). */
  expiresInSeconds: number;
  user: CurrentUser;
}
