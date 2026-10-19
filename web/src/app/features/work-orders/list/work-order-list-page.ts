import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { catchError, map, merge, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth-store';
import { LoadState } from '../../../core/load-state';
import { problemMessage, toProblem } from '../../../core/problem-details';
import {
  PagedResult,
  PRIORITIES,
  STATUSES,
  WorkOrderFilter,
  WorkOrderPriority,
  WorkOrderStatus,
  WorkOrderSummary,
} from '../work-order.models';
import { WorkOrdersApi } from '../work-orders-api';
import { FilterChange, WorkOrderFilters } from './work-order-filters';
import { WorkOrderTable } from './work-order-table';

const PAGE_SIZE_OPTIONS = [20, 50, 100];

/**
 * Smart component: owns the data. The URL query string is the single source of truth for filters, so a filtered
 * view can be bookmarked or shared and the back button works. Presentational children get data in and send events out.
 */
@Component({
  selector: 'po-work-order-list-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatPaginatorModule, MatProgressBarModule, WorkOrderFilters, WorkOrderTable],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-list-page.html',
  styleUrl: './work-order-list-page.scss',
})
export class WorkOrderListPage {
  readonly #api = inject(WorkOrdersApi);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #route = inject(ActivatedRoute);
  readonly #retry$ = new Subject<void>();

  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  protected readonly isTechnician = computed(() => this.#auth.hasAnyRole(['Technician']));
  // toSignal re-throws an observable's error whenever the signal is read, which would break the template, so a
  // failed load is turned into a value: the unit filter is simply empty.
  protected readonly plantUnits = toSignal(this.#api.plantUnits().pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly filter = toSignal(this.#route.queryParamMap.pipe(map((params) => this.#parse(params))), {
    requireSync: true,
  });

  /** A request for new filters or a new page is in flight while the previous results stay on screen. */
  protected readonly refreshing = signal(false);

  // switchMap cancels the previous request when filters change quickly, so an old, slow response can never
  // overwrite a newer one. The skeleton shows only on the first load; later loads keep the table and paginator
  // mounted (so keyboard focus stays on "Next page") and show a progress bar instead.
  protected readonly result = toSignal(
    merge(this.#route.queryParamMap, this.#retry$.pipe(map(() => this.#route.snapshot.queryParamMap))).pipe(
      map((params) => this.#parse(params)),
      tap(() => this.refreshing.set(true)),
      switchMap((filter) =>
        this.#api.list(filter).pipe(
          map((data): LoadState<PagedResult<WorkOrderSummary>> => ({ status: 'loaded', data })),
          catchError((error: unknown) => of<LoadState<never>>({ status: 'error', problem: toProblem(error) })),
        ),
      ),
      tap(() => this.refreshing.set(false)),
      startWith<LoadState<PagedResult<WorkOrderSummary>>>({ status: 'loading' }),
    ),
    { initialValue: { status: 'loading' } },
  );

  protected readonly hasFilters = computed(() => {
    const f = this.filter();
    return !f.activeOnly || f.status !== null || f.priority !== null || f.plantUnitId !== null || f.assignedToUserId !== null;
  });

  protected readonly errorMessage = computed(() => {
    const result = this.result();
    return result.status === 'error' ? problemMessage(result.problem) : '';
  });

  // Captured once per render so every row's age is measured against the same moment.
  protected readonly now = computed(() => (this.result(), Date.now()));

  protected changeFilter(change: FilterChange): void {
    void this.#router.navigate([], { queryParams: { ...change, page: null }, queryParamsHandling: 'merge' });
  }

  protected clearFilters(): void {
    void this.#router.navigate([], { queryParams: {} });
  }

  protected changePage(event: PageEvent): void {
    void this.#router.navigate([], {
      queryParams: { page: event.pageIndex + 1, size: event.pageSize === PAGE_SIZE_OPTIONS[0] ? null : event.pageSize },
      queryParamsHandling: 'merge',
    });
  }

  protected retry(): void {
    this.#retry$.next();
  }

  protected firstPage(): void {
    void this.#router.navigate([], { queryParams: { page: null }, queryParamsHandling: 'merge' });
  }

  #parse(params: ParamMap): WorkOrderFilter {
    const status = params.get('status');
    const priority = params.get('priority');
    const page = Number(params.get('page'));
    const size = Number(params.get('size'));
    return {
      // No status in the URL means the default "active work" view; "all" includes closed work orders.
      activeOnly: status !== 'all',
      status: STATUSES.includes(status as WorkOrderStatus) ? (status as WorkOrderStatus) : null,
      priority: PRIORITIES.includes(priority as WorkOrderPriority) ? (priority as WorkOrderPriority) : null,
      plantUnitId: params.get('unit'),
      assignedToUserId: params.get('mine') === 'true' ? (this.#auth.user()?.id ?? null) : null,
      page: Number.isInteger(page) && page > 0 ? page : 1,
      pageSize: PAGE_SIZE_OPTIONS.includes(size) ? size : PAGE_SIZE_OPTIONS[0],
    };
  }
}
