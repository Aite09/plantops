import { InjectionToken } from '@angular/core';

/**
 * Settings read at start-up from /config.json instead of being compiled in, so one build runs anywhere:
 * the dev server serves public/config.json, the Docker image generates it from environment variables, and the
 * Azure deployment writes it (with the public demo password) before upload.
 */
export interface AppConfig {
  apiBaseUrl: string;
  showDemoAccounts: boolean;
  /** Shared password of the public demo accounts, shown on the login page. Null locally, where it is a user-secret. */
  demoPassword: string | null;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

export async function loadAppConfig(): Promise<AppConfig> {
  const response = await fetch('/config.json', { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Could not load /config.json (HTTP ${response.status}).`);
  }
  const config = (await response.json()) as Partial<AppConfig>;
  return {
    // Trailing slashes removed so URLs are always built as `${apiBaseUrl}/api/...`.
    apiBaseUrl: (config.apiBaseUrl ?? '').replace(/\/+$/, ''),
    showDemoAccounts: config.showDemoAccounts === true,
    demoPassword: typeof config.demoPassword === 'string' && config.demoPassword !== '' ? config.demoPassword : null,
  };
}
