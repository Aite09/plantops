import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { APP_CONFIG } from '../../core/app-config';
import { AuthStore } from '../../core/auth/auth-store';
import { problemMessage, toProblem } from '../../core/problem-details';

const DEMO_ACCOUNTS = [
  { email: 'staff@plantops.test', role: 'Staff' },
  { email: 'supervisor@plantops.test', role: 'Supervisor' },
  { email: 'tech.a@plantops.test', role: 'Technician' },
  { email: 'admin@plantops.test', role: 'Admin' },
];

@Component({
  selector: 'po-login-page',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
})
export class LoginPage {
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #config = inject(APP_CONFIG);

  /** Query parameters, bound by the router (withComponentInputBinding). */
  readonly returnUrl = input<string>();
  readonly reason = input<string>();

  // Typed reactive form: form.getRawValue() is { email: string; password: string }, checked by the compiler.
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  /** The public demo's shared password, from /config.json. Null locally, where it is the developer's user-secret. */
  protected readonly demoPassword = this.#config.demoPassword;
  /** The public demo has no Admin account (an Admin could create accounts), so Admin is only offered locally. */
  protected readonly demoAccounts = !this.#config.showDemoAccounts
    ? []
    : this.demoPassword
      ? DEMO_ACCOUNTS.filter((account) => account.role !== 'Admin')
      : DEMO_ACCOUNTS;

  protected useDemoAccount(email: string): void {
    // With the public password known, one click fills in the whole form; locally only the email.
    this.form.patchValue(this.demoPassword ? { email, password: this.demoPassword } : { email });
    document.getElementById('password')?.focus();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();

    this.#auth
      .login(email, password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => void this.#router.navigateByUrl(safeReturnUrl(this.returnUrl())),
        error: (error: unknown) => {
          const problem = toProblem(error);
          this.error.set(problem.status === 401 ? 'Email or password is incorrect.' : problemMessage(problem));
        },
      });
  }
}

/** Only return to paths inside this app, never to another site (open-redirect protection). */
function safeReturnUrl(url: string | undefined): string {
  return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\') ? url : '/work-orders';
}
