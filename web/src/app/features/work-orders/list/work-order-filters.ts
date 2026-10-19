import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { PlantUnit, PRIORITIES, STATUS_LABELS, STATUSES, WorkOrderFilter } from '../work-order.models';

/** Query-string changes the filter bar asks for; null removes the parameter. */
export interface FilterChange {
  status?: string | null;
  priority?: string | null;
  unit?: string | null;
  mine?: 'true' | null;
}

/**
 * Presentational component: shows the current filter and reports what the user picked. It holds no state and
 * calls no API, so the page decides what a change means (here: update the URL).
 */
@Component({
  selector: 'po-work-order-filters',
  imports: [MatFormFieldModule, MatSelectModule, MatSlideToggleModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters" role="group" aria-label="Filter work orders">
      <mat-form-field class="status">
        <mat-label>Status</mat-label>
        <mat-select [value]="statusValue()" (selectionChange)="changed.emit({ status: $event.value === 'active' ? null : $event.value })">
          <mat-option value="active">Active (not closed)</mat-option>
          <mat-option value="all">All, including closed</mat-option>
          @for (status of statuses; track status) {
            <mat-option [value]="status">{{ statusLabels[status] }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field>
        <mat-label>Priority</mat-label>
        <mat-select [value]="filter().priority ?? ''" (selectionChange)="changed.emit({ priority: $event.value || null })">
          <mat-option value="">Any priority</mat-option>
          @for (priority of priorities; track priority) {
            <mat-option [value]="priority">{{ priority }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field>
        <mat-label>Plant unit</mat-label>
        <mat-select [value]="filter().plantUnitId ?? ''" (selectionChange)="changed.emit({ unit: $event.value || null })">
          <mat-option value="">All units</mat-option>
          @for (unit of plantUnits(); track unit.id) {
            <mat-option [value]="unit.id"><span class="mono">{{ unit.code }}</span> {{ unit.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (showMine()) {
        <mat-slide-toggle
          [checked]="filter().assignedToUserId !== null"
          (change)="changed.emit({ mine: $event.checked ? 'true' : null })"
        >
          Assigned to me
        </mat-slide-toggle>
      }

      @if (hasFilters()) {
        <button mat-button type="button" (click)="cleared.emit()">
          <mat-icon>filter_alt_off</mat-icon>
          Clear filters
        </button>
      }
    </div>
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
    }
    mat-form-field {
      width: 200px;
    }
    mat-form-field.status {
      width: 220px;
    }
    @media (max-width: 600px) {
      mat-form-field,
      mat-form-field.status {
        width: 100%;
      }
    }
  `,
})
export class WorkOrderFilters {
  readonly filter = input.required<WorkOrderFilter>();
  readonly plantUnits = input<PlantUnit[]>([]);
  readonly showMine = input(false);
  readonly hasFilters = input(false);

  readonly changed = output<FilterChange>();
  readonly cleared = output<void>();

  protected readonly statuses = STATUSES;
  protected readonly priorities = PRIORITIES;
  protected readonly statusLabels = STATUS_LABELS;

  protected statusValue(): string {
    const filter = this.filter();
    return filter.status ?? (filter.activeOnly ? 'active' : 'all');
  }
}
