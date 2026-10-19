import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { catchError, filter, forkJoin, map, Observable, of, startWith, Subject, switchMap, take, tap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth-store';
import { LoadState } from '../../../core/load-state';
import { problemMessage, toProblem } from '../../../core/problem-details';
import { PriorityBadge } from '../../../shared/priority-badge';
import { StatusBadge } from '../../../shared/status-badge';
import { UsersApi } from '../../users/users-api';
import { availableActions } from '../work-order-actions';
import { AuditEntry, WorkOrderDetails } from '../work-order.models';
import { WorkOrdersApi } from '../work-orders-api';
import { WorkOrderActionBar } from './work-order-action-bar';
import { WorkOrderFacts } from './work-order-facts';
import { WorkOrderHistory } from './work-order-history';

interface DetailView {
  workOrder: WorkOrderDetails;
  history: AuditEntry[];
}

const NO_ACTIONS = { edit: false, approve: false, assign: false, start: false, close: false };

@Component({
  selector: 'po-work-order-detail-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    PriorityBadge,
    StatusBadge,
    WorkOrderActionBar,
    WorkOrderFacts,
    WorkOrderHistory,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-detail-page.html',
  styleUrl: './work-order-detail-page.scss',
})
export class WorkOrderDetailPage {
  readonly #api = inject(WorkOrdersApi);
  readonly #usersApi = inject(UsersApi);
  readonly #auth = inject(AuthStore);
  readonly #snackBar = inject(MatSnackBar);
  readonly #reload$ = new Subject<void>();

  /** The :id route parameter, bound by the router. */
  readonly id = input.required<string>();

  // First load shows the skeleton; reloads after an action keep the current page on screen until fresh data arrives.
  protected readonly view = toSignal(
    toObservable(this.id).pipe(
      switchMap((id) =>
        this.#reload$.pipe(
          startWith(undefined),
          switchMap(() => this.#load(id)),
          // Buttons re-enable only once they reflect the fresh status, not when the action's request returns.
          tap(() => this.busy.set(null)),
          startWith<LoadState<DetailView>>({ status: 'loading' }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } },
  );

  protected readonly actions = computed(() => {
    const view = this.view();
    return view.status === 'loaded' ? availableActions(view.data.workOrder, this.#auth.user()) : NO_ACTIONS;
  });

  // Technicians are fetched only when this user can actually assign, and only once.
  protected readonly technicians = toSignal(
    toObservable(computed(() => this.actions().assign)).pipe(
      filter(Boolean),
      take(1),
      switchMap(() => this.#usersApi.list('Technician')),
      catchError(() => of([])),
    ),
    { initialValue: [] },
  );

  /** Names for user ids that appear in the history (assignees, reporters, people who made changes). */
  protected readonly userNames = computed(() => {
    const names: Record<string, string> = {};
    for (const technician of this.technicians()) names[technician.id] = technician.displayName;
    const view = this.view();
    if (view.status === 'loaded') {
      const wo = view.data.workOrder;
      if (wo.reportedByName) names[wo.reportedByUserId] = wo.reportedByName;
      if (wo.approvedByUserId && wo.approvedByName) names[wo.approvedByUserId] = wo.approvedByName;
      if (wo.assignedToUserId && wo.assignedToName) names[wo.assignedToUserId] = wo.assignedToName;
      for (const entry of view.data.history) if (entry.userId && entry.userName) names[entry.userId] = entry.userName;
    }
    return names;
  });

  /** Which action is running, so its button shows progress and the others wait. */
  protected readonly busy = signal<string | null>(null);

  protected approve(): void {
    this.#run('approve', this.#api.approve(this.id()), 'Work order approved.');
  }

  protected assign(technicianId: string): void {
    const name = this.userNames()[technicianId] ?? 'technician';
    this.#run('assign', this.#api.assign(this.id(), technicianId), `Assigned to ${name}.`);
  }

  protected start(): void {
    this.#run('start', this.#api.start(this.id()), 'Work started.');
  }

  protected close(notes: string): void {
    this.#run('close', this.#api.close(this.id(), notes), 'Work order closed.');
  }

  protected retry(): void {
    this.#reload$.next();
  }

  protected errorMessage(): string {
    const view = this.view();
    return view.status === 'error' ? problemMessage(view.problem) : '';
  }

  #load(id: string): Observable<LoadState<DetailView>> {
    return forkJoin({ workOrder: this.#api.get(id), history: this.#api.history(id) }).pipe(
      map((data): LoadState<DetailView> => ({ status: 'loaded', data })),
      catchError((error: unknown) => of<LoadState<DetailView>>({ status: 'error', problem: toProblem(error) })),
    );
  }

  #run(action: string, request: Observable<void>, successMessage: string): void {
    this.busy.set(action);
    request.subscribe({
      next: () => {
        this.#snackBar.open(successMessage, undefined, { duration: 4000 });
        this.#reload$.next();
      },
      error: (error: unknown) => {
        // The API is the authority: show its reason, then reload in case someone else changed the work order.
        this.#snackBar.open(problemMessage(toProblem(error)), 'Dismiss', { duration: 8000 });
        this.#reload$.next();
      },
    });
  }
}
