import { vi } from 'vitest';
import { loadAppConfig } from './app-config';

describe('loadAppConfig', () => {
  afterEach(() => vi.unstubAllGlobals());

  function serveConfig(body: unknown, status = 200): void {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: status >= 200 && status < 300, status, json: () => Promise.resolve(body) }),
    );
  }

  it('reads the deployed settings, including the public demo password', async () => {
    serveConfig({ apiBaseUrl: 'https://api.example.test/', showDemoAccounts: true, demoPassword: 'Demo-1a2b3c4d-Pw1' });

    await expect(loadAppConfig()).resolves.toEqual({
      apiBaseUrl: 'https://api.example.test',
      showDemoAccounts: true,
      demoPassword: 'Demo-1a2b3c4d-Pw1',
    });
  });

  it.each([undefined, null, '', 42])('treats a demo password of %s as none, as in local development', async (demoPassword) => {
    serveConfig({ apiBaseUrl: 'http://localhost:5297', showDemoAccounts: true, demoPassword });

    expect((await loadAppConfig()).demoPassword).toBeNull();
  });

  it('fails when config.json cannot be loaded', async () => {
    serveConfig({}, 404);

    await expect(loadAppConfig()).rejects.toThrow('HTTP 404');
  });
});
