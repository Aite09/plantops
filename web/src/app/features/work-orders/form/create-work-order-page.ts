import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { ProblemDetails, toProblem } from '../../../core/problem-details';
import { WorkOrderInput } from '../work-order.models';
import { WorkOrdersApi } from '../work-orders-api';
import { WorkOrderForm } from './work-order-form';

/** Smart page: loads reference data, submits to the API, and navigates. The form itself is presentational. */
@Component({
  selector: 'po-create-work-order-page',
  imports: [WorkOrderForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Report a fault</h1>
          <p class="muted lead">A supervisor reviews new faults and assigns the work.</p>
        </div>
      </header>
      @if (plantUnitsFailed()) {
        <p class="load-error" role="alert">Couldn't load the plant units. Reload the page to try again.</p>
      }
      <po-work-order-form
        mode="create"
        [plantUnits]="plantUnits()"
        [submitting]="submitting()"
        [problem]="problem()"
        (save)="save($event)"
        (cancelled)="cancel()"
      />
    </div>
  `,
  styles: `
    .lead {
      margin: 4px 0 0;
    }
    .load-error {
      margin: 0 0 16px;
      color: var(--po-critical-ink);
    }
  `,
})
export class CreateWorkOrderPage {
  readonly #api = inject(WorkOrdersApi);
  readonly #router = inject(Router);
  readonly #snackBar = inject(MatSnackBar);

  protected readonly plantUnitsFailed = signal(false);
  // toSignal re-throws an observable's error whenever the signal is read, so the failure becomes a value instead.
  protected readonly plantUnits = toSignal(
    this.#api.plantUnits().pipe(
      catchError(() => {
        this.plantUnitsFailed.set(true);
        return of([]);
      }),
    ),
    { initialValue: [] },
  );
  protected readonly submitting = signal(false);
  protected readonly problem = signal<ProblemDetails | null>(null);

  protected save(input: WorkOrderInput): void {
    this.submitting.set(true);
    this.problem.set(null);
    this.#api
      .create(input)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: ({ id }) => {
          this.#snackBar.open('Fault reported.', undefined, { duration: 4000 });
          void this.#router.navigate(['/work-orders', id]);
        },
        error: (error: unknown) => this.problem.set(toProblem(error)),
      });
  }

  protected cancel(): void {
    void this.#router.navigate(['/work-orders']);
  }
}
