import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG, AppConfig } from '../../core/app-config';
import { AuthStore } from '../../core/auth/auth-store';
import { LoginPage } from './login-page';

describe('LoginPage demo accounts', () => {
  const PUBLIC_PASSWORD = 'Demo-1a2b3c4d-Pw1';

  async function render(config: Partial<AppConfig>): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        {
          provide: APP_CONFIG,
          useValue: { apiBaseUrl: 'http://localhost:5297', showDemoAccounts: true, demoPassword: null, ...config },
        },
        // Signing in isn't exercised here; the page only needs something to inject.
        { provide: AuthStore, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(LoginPage);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function listedEmails(page: HTMLElement): string[] {
    return [...page.querySelectorAll('.demo-account .mono')].map((element) => element.textContent?.trim() ?? '');
  }

  function field(page: HTMLElement, id: string): HTMLInputElement {
    return page.querySelector(`#${id}`) as HTMLInputElement;
  }

  it('locally lists every demo account, Admin included, and points to the user-secret', async () => {
    const page = await render({});

    expect(listedEmails(page)).toContain('admin@plantops.test');
    expect(page.querySelector('.demo')?.textContent).toContain('Seed:DemoUserPassword');
  });

  it('locally fills in only the email when an account is picked', async () => {
    const page = await render({});

    (page.querySelector('.demo-account') as HTMLButtonElement).click();

    expect(field(page, 'email').value).toBe('staff@plantops.test');
    expect(field(page, 'password').value).toBe('');
  });

  it('on the public demo shows the shared password and leaves out Admin', async () => {
    const page = await render({ demoPassword: PUBLIC_PASSWORD });

    expect(listedEmails(page)).toEqual(['staff@plantops.test', 'supervisor@plantops.test', 'tech.a@plantops.test']);
    expect(page.querySelector('.demo')?.textContent).toContain(PUBLIC_PASSWORD);
  });

  it('on the public demo fills in the email and the password with one click', async () => {
    const page = await render({ demoPassword: PUBLIC_PASSWORD });

    (page.querySelector('.demo-account') as HTMLButtonElement).click();

    expect(field(page, 'email').value).toBe('staff@plantops.test');
    expect(field(page, 'password').value).toBe(PUBLIC_PASSWORD);
  });

  it('shows no demo accounts when config.json turns them off', async () => {
    const page = await render({ showDemoAccounts: false, demoPassword: PUBLIC_PASSWORD });

    expect(page.querySelector('.demo')).toBeNull();
  });
});
