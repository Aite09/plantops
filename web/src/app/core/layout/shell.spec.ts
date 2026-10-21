import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CurrentUser, Role } from '../auth/auth.models';
import { AuthStore } from '../auth/auth-store';
import { Shell } from './shell';

describe('Shell navigation', () => {
  async function navLinks(...roles: Role[]): Promise<string[]> {
    const user: CurrentUser = { id: 'u-1', email: 'someone@plantops.test', displayName: 'Some One', roles };
    TestBed.configureTestingModule({
      imports: [Shell],
      providers: [
        provideRouter([]),
        // The shell reads the user and asks about roles; signing out isn't exercised here.
        {
          provide: AuthStore,
          useValue: {
            user: signal(user),
            hasAnyRole: (wanted: readonly Role[]) => wanted.some((role) => roles.includes(role)),
            logout: () => undefined,
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
    return [...(nav?.querySelectorAll('a') ?? [])].map((link) => link.textContent?.trim() ?? '');
  }

  it.each<[Role, string[]]>([
    ['Staff', ['Work orders']],
    ['Technician', ['Work orders']],
    ['Supervisor', ['Work orders', 'Dashboard']],
    ['Admin', ['Work orders', 'Dashboard', 'Users']],
  ])('a %s sees %j', async (role, expected) => {
    expect(await navLinks(role)).toEqual(expected);
  });
});
