import { DatePipe, DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { catchError, filter, fromEvent, map, merge, Observable, of, scan, startWith, Subject, switchMap, tap } from 'rxjs';
import { ProblemDetails, problemMessage, toProblem } from '../../core/problem-details';
import { WorkOrderTable } from '../work-orders/list/work-order-table';
import { Dashboard, DashboardApi } from './dashboard-api';
import { TechnicianWorkloadTable } from './technician-workload-table';
import { UnitWorkloadTable } from './unit-workload-table';

/**
 * What the page shows. Unlike a plain LoadState, a failed refresh keeps the last numbers on screen (with the reason it
 * could not refresh) instead of replacing them with an error: a brief network drop must not blank a supervisor's view.
 */
type DashboardView =
  | { status: 'loading' }
  | { status: 'error'; problem: ProblemDetails }
  | { status: 'loaded'; data: Dashboard; refreshProblem: ProblemDetails | null };

type Outcome = { data: Dashboard } | { problem: ProblemDetails };

/**
 * Smart component for supervisors and admins (roleGuard on the route, ManageWorkOrders policy on the API): the queue
 * of work that needs attention first, then where open work sits and who holds it. No headline numbers: every count is
 * in a table, and the unit table's counts link to the rows behind them.
 */
@Component({
  selector: 'po-dashboard-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, WorkOrderTable, UnitWorkloadTable, TechnicianWorkloadTable],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  readonly #api = inject(DashboardApi);
  readonly #document = inject(DOCUMENT);
  readonly #refresh$ = new Subject<void>();

  // Coming back to this tab asks again, so a dashboard left open behind other windows catches up when it is looked at.
  // No polling: within 60 seconds the API answers from its output cache, and the page says how old the numbers are.
  readonly #backInView$: Observable<unknown> = fromEvent(this.#document, 'visibilitychange').pipe(
    filter(() => this.#document.visibilityState === 'visible'),
  );

  /** A request is in flight while the current numbers (or error) stay on screen. */
  protected readonly refreshing = signal(false);

  // The skeleton shows only on the first load. Each answer is folded into the view: success replaces it; a failure
  // after a success keeps the numbers and records why. Errors become values because toSignal re-throws.
  protected readonly view = toSignal(
    merge(this.#refresh$, this.#backInView$).pipe(
      startWith(undefined),
      tap(() => this.refreshing.set(true)),
      switchMap(() =>
        this.#api.get().pipe(
          map((data): Outcome => ({ data })),
          catchError((error: unknown) => of<Outcome>({ problem: toProblem(error) })),
        ),
      ),
      tap(() => this.refreshing.set(false)),
      scan((view: DashboardView, outcome: Outcome): DashboardView => {
        if ('data' in outcome) return { status: 'loaded', data: outcome.data, refreshProblem: null };
        if (view.status === 'loaded') return { ...view, refreshProblem: outcome.problem };
        return { status: 'error', problem: outcome.problem };
      }, { status: 'loading' }),
      startWith<DashboardView>({ status: 'loading' }),
    ),
    { initialValue: { status: 'loading' } },
  );

  // Ages are measured at the moment the API took the snapshot, so they agree with what it selected as ageing.
  protected readonly snapshotTime = computed(() => {
    const view = this.view();
    return view.status === 'loaded' ? Date.parse(view.data.generatedAtUtc) : 0;
  });

  protected readonly errorMessage = computed(() => {
    const view = this.view();
    if (view.status === 'error') return problemMessage(view.problem);
    return view.status === 'loaded' && view.refreshProblem ? problemMessage(view.refreshProblem) : '';
  });

  /** One button for Refresh and Try again, so the element that has keyboard focus is never removed. */
  protected readonly refreshLabel = computed(() => {
    if (this.refreshing()) return 'Refreshing…';
    return this.view().status === 'error' ? 'Try again' : 'Refresh';
  });

  protected refresh(): void {
    // The button stays focusable while disabled (disabledInteractive), so a press during a refresh must do nothing.
    if (!this.refreshing()) {
      this.#refresh$.next();
    }
  }
}
