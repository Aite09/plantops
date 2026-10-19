import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { FormGroupDirective, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Role } from '../../core/auth/auth.models';
import { applyServerErrors, ProblemDetails, problemMessage } from '../../core/problem-details';
import { NewUser } from './users-api';

const ROLES: readonly Role[] = ['Staff', 'Supervisor', 'Technician', 'Admin'];

/** Presentational: typed form for a new account. Password rules are enforced by the API (ASP.NET Core Identity). */
@Component({
  selector: 'po-create-user-form',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form #formDirective="ngForm" [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <mat-form-field>
        <mat-label>Name</mat-label>
        <input matInput formControlName="displayName" autocomplete="off" />
        @if (form.controls.displayName.hasError('required')) {
          <mat-error>Enter the person's name.</mat-error>
        } @else if (form.controls.displayName.hasError('maxlength')) {
          <mat-error>Use 100 characters or fewer.</mat-error>
        } @else if (form.controls.displayName.hasError('server')) {
          <mat-error>{{ form.controls.displayName.getError('server') }}</mat-error>
        }
      </mat-form-field>

      <mat-form-field>
        <mat-label>Email</mat-label>
        <input matInput type="email" formControlName="email" autocomplete="off" />
        @if (form.controls.email.hasError('required')) {
          <mat-error>Enter an email address.</mat-error>
        } @else if (form.controls.email.hasError('email')) {
          <mat-error>Enter a valid email address.</mat-error>
        } @else if (form.controls.email.hasError('server')) {
          <mat-error>{{ form.controls.email.getError('server') }}</mat-error>
        }
      </mat-form-field>

      <mat-form-field>
        <mat-label>Role</mat-label>
        <mat-select formControlName="role">
          @for (role of roles; track role) {
            <mat-option [value]="role">{{ role }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field>
        <mat-label>Temporary password</mat-label>
        <input matInput type="password" formControlName="password" autocomplete="new-password" />
        <mat-hint>10+ characters with upper and lower case, a number and a symbol.</mat-hint>
        @if (form.controls.password.hasError('required')) {
          <mat-error>Set a temporary password.</mat-error>
        } @else if (form.controls.password.hasError('server')) {
          <mat-error>{{ form.controls.password.getError('server') }}</mat-error>
        }
      </mat-form-field>

      @if (formError(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <button mat-flat-button type="submit" [disabled]="submitting()">
        {{ submitting() ? 'Creating…' : 'Create account' }}
      </button>
    </form>
  `,
  styles: `
    form {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .error {
      margin: 0;
      color: var(--po-critical-ink);
    }
    button {
      height: 44px;
    }
  `,
})
export class CreateUserForm {
  readonly submitting = input(false);
  readonly problem = input<ProblemDetails | null>(null);
  /** Changes after a successful create; the form clears itself then. */
  readonly resetSignal = input(0);

  readonly save = output<NewUser>();

  protected readonly roles = ROLES;
  protected readonly formError = signal<string | null>(null);
  private readonly formDirective = viewChild.required<FormGroupDirective>('formDirective');

  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    role: ['Staff' as Role, Validators.required],
    password: ['', Validators.required],
  });

  constructor() {
    // resetForm() on the directive, not form.reset(): it also clears the "submitted" flag Material uses to show
    // errors, so the emptied fields don't all turn red next to the "Account created" message.
    effect(() => {
      if (this.resetSignal() > 0) {
        untracked(() => this.formDirective().resetForm());
      }
    });

    effect(() => {
      const problem = this.problem();
      if (!problem) {
        this.formError.set(null);
        return;
      }
      const unmatched = applyServerErrors(this.form, problem);
      const hasFieldErrors = Object.keys(problem.errors ?? {}).length > unmatched.length;
      this.formError.set(unmatched.length > 0 ? unmatched.join(' ') : hasFieldErrors ? null : problemMessage(problem));
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.save.emit(this.form.getRawValue());
  }
}
