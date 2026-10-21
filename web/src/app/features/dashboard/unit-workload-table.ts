import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { STATUS_LABELS, WorkOrderStatus } from '../work-orders/work-order.models';
import { UnitWorkload } from './dashboard-api';

type CountKey = 'open' | 'approved' | 'assigned' | 'inProgress';

const COLUMNS: readonly { status: WorkOrderStatus; key: CountKey }[] = [
  { status: 'Open', key: 'open' },
  { status: 'Approved', key: 'approved' },
  { status: 'Assigned', key: 'assigned' },
  { status: 'InProgress', key: 'inProgress' },
];

/**
 * Presentational: open work per plant unit and status. Every non-zero count links to the work-order list filtered the
 * same way, so each of these numbers is one click from the rows behind it.
 */
@Component({
  selector: 'po-unit-workload-table',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table>
        <caption class="visually-hidden">Open work orders per plant unit and status</caption>
        <thead>
          <tr>
            <th scope="col">Unit</th>
            @for (column of columns; track column.status) {
              <th scope="col" class="num">{{ labels[column.status] }}</th>
            }
            <th scope="col" class="num">Total</th>
          </tr>
        </thead>
        <tbody>
          @for (unit of units(); track unit.plantUnitId) {
            <tr>
              <th scope="row">
                <span class="mono">{{ unit.plantUnitCode }}</span>
                <span class="name">{{ unit.plantUnitName }}</span>
              </th>
              @for (column of columns; track column.status) {
                <td class="num">
                  @if (unit[column.key] > 0) {
                    <a routerLink="/work-orders" [queryParams]="{ unit: unit.plantUnitId, status: column.status }">
                      {{ unit[column.key] }}<span class="visually-hidden"> {{ labels[column.status] }} on {{ unit.plantUnitCode }}</span>
                    </a>
                  } @else {
                    <span class="zero">0</span>
                  }
                </td>
              }
              <td class="num total">
                @if (total(unit) > 0) {
                  <a routerLink="/work-orders" [queryParams]="{ unit: unit.plantUnitId }">
                    {{ total(unit) }}<span class="visually-hidden"> open on {{ unit.plantUnitCode }}</span>
                  </a>
                } @else {
                  <span class="zero">0</span>
                }
              </td>
            </tr>
          }
        </tbody>
        <tfoot>
          <tr>
            <th scope="row">All units</th>
            @for (column of columns; track column.status) {
              <td class="num">
                @if (totals()[column.key] > 0) {
                  <a routerLink="/work-orders" [queryParams]="{ status: column.status }">
                    {{ totals()[column.key] }}<span class="visually-hidden"> {{ labels[column.status] }} in all units</span>
                  </a>
                } @else {
                  <span class="zero">0</span>
                }
              </td>
            }
            <td class="num total">
              @if (totals().all > 0) {
                <a routerLink="/work-orders">{{ totals().all }}<span class="visually-hidden"> open in all units</span></a>
              } @else {
                <span class="zero">0</span>
              }
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  `,
  styleUrl: './unit-workload-table.scss',
})
export class UnitWorkloadTable {
  readonly units = input.required<UnitWorkload[]>();

  protected readonly columns = COLUMNS;
  protected readonly labels = STATUS_LABELS;

  protected readonly totals = computed(() => {
    const sums: Record<CountKey | 'all', number> = { open: 0, approved: 0, assigned: 0, inProgress: 0, all: 0 };
    for (const unit of this.units()) {
      for (const { key } of COLUMNS) {
        sums[key] += unit[key];
        sums.all += unit[key];
      }
    }
    return sums;
  });

  protected total(unit: UnitWorkload): number {
    return unit.open + unit.approved + unit.assigned + unit.inProgress;
  }
}
