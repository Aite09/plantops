import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { catchError, finalize, map, of, startWith, Subject, switchMap } from 'rxjs';
import { LoadState } from '../../core/load-state';
import { ProblemDetails, problemMessage, toProblem } from '../../core/problem-details';
import { CreateUserForm } from './create-user-form';
import { NewUser, UsersApi, UserSummary } from './users-api';

/** Admin-only page (roleGuard on the route, ManageUsers policy on the API): list accounts and create new ones. */
@Component({
  selector: 'po-users-page',
  imports: [MatTableModule, MatButtonModule, MatIconModule, CreateUserForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
  styleUrl: './users-page.scss',
})
export class UsersPage {
  readonly #api = inject(UsersApi);
  readonly #snackBar = inject(MatSnackBar);
  readonly #reload$ = new Subject<void>();

  protected readonly columns = ['name', 'email', 'roles'];
  protected readonly users = toSignal(
    this.#reload$.pipe(
      startWith(undefined),
      switchMap(() =>
        this.#api.list().pipe(
          map((data): LoadState<UserSummary[]> => ({ status: 'loaded', data })),
          catchError((error: unknown) => of<LoadState<UserSummary[]>>({ status: 'error', problem: toProblem(error) })),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as LoadState<UserSummary[]> },
  );

  protected readonly submitting = signal(false);
  protected readonly problem = signal<ProblemDetails | null>(null);
  /** Bumped after a successful create, so the form clears itself. */
  protected readonly created = signal(0);

  protected create(user: NewUser): void {
    this.submitting.set(true);
    this.problem.set(null);
    this.#api
      .create(user)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.#snackBar.open(`Account created for ${user.displayName}.`, undefined, { duration: 4000 });
          this.created.update((n) => n + 1);
          this.#reload$.next();
        },
        error: (error: unknown) => this.problem.set(toProblem(error)),
      });
  }

  protected retry(): void {
    this.#reload$.next();
  }

  protected errorMessage(): string {
    const users = this.users();
    return users.status === 'error' ? problemMessage(users.problem) : '';
  }
}
