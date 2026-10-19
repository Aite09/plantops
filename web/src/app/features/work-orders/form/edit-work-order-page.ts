import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { catchError, finalize, map, of, startWith, Subject, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth-store';
import { LoadState } from '../../../core/load-state';
import { ProblemDetails, problemMessage, toProblem } from '../../../core/problem-details';
import { availableActions } from '../work-order-actions';
import { WorkOrderDetails, WorkOrderInput } from '../work-order.models';
import { WorkOrdersApi } from '../work-orders-api';
import { WorkOrderForm } from './work-order-form';

@Component({
  selector: 'po-edit-work-order-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, WorkOrderForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './edit-work-order-page.html',
  styleUrl: './edit-work-order-page.scss',
})
export class EditWorkOrderPage {
  readonly #api = inject(WorkOrdersApi);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #snackBar = inject(MatSnackBar);
  readonly #reload$ = new Subject<void>();

  readonly id = input.required<string>();

  // Only used to label the locked unit/equipment fields, so a failed load just leaves them blank. (toSignal would
  // otherwise re-throw the error whenever the signal is read and break the template.)
  protected readonly plantUnits = toSignal(this.#api.plantUnits().pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly view = toSignal(
    toObservable(this.id).pipe(
      switchMap((id) =>
        this.#reload$.pipe(
          startWith(undefined),
          switchMap(() =>
            this.#api.get(id).pipe(
              map((data): LoadState<WorkOrderDetails> => ({ status: 'loaded', data })),
              catchError((error: unknown) => of<LoadState<WorkOrderDetails>>({ status: 'error', problem: toProblem(error) })),
              startWith<LoadState<WorkOrderDetails>>({ status: 'loading' }),
            ),
          ),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } },
  );

  protected readonly canEdit = computed(() => {
    const view = this.view();
    return view.status === 'loaded' && availableActions(view.data, this.#auth.user()).edit;
  });

  protected readonly submitting = signal(false);
  protected readonly problem = signal<ProblemDetails | null>(null);
  /** Someone else saved this work order after it was loaded here (HTTP 409 from the rowversion check). */
  protected readonly conflict = signal(false);

  protected save(input: WorkOrderInput): void {
    const view = this.view();
    if (view.status !== 'loaded') return;

    this.submitting.set(true);
    this.problem.set(null);
    this.conflict.set(false);
    const { title, description, priority } = input;

    this.#api
      .update(view.data.id, { title, description, priority }, view.data.version)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.#snackBar.open('Changes saved.', undefined, { duration: 4000 });
          void this.#router.navigate(['/work-orders', view.data.id]);
        },
        error: (error: unknown) => {
          const problem = toProblem(error);
          if (problem.code === 'Concurrency.Conflict') {
            this.conflict.set(true);
          } else {
            this.problem.set(problem);
          }
        },
      });
  }

  protected retry(): void {
    this.#reload$.next();
  }

  protected reloadLatest(): void {
    this.conflict.set(false);
    this.#reload$.next();
  }

  protected cancel(): void {
    void this.#router.navigate(['/work-orders', this.id()]);
  }

  protected errorMessage(): string {
    const view = this.view();
    return view.status === 'error' ? problemMessage(view.problem) : '';
  }
}
