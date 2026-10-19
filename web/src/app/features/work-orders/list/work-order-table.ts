import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { PriorityBadge } from '../../../shared/priority-badge';
import { StatusBadge } from '../../../shared/status-badge';
import { AGEING_DAYS, ageInDays, formatAge } from '../../../shared/time';
import { WorkOrderSummary } from '../work-order.models';

/** Presentational table of work orders. Each row's title is a real link, so keyboard and middle-click work. */
@Component({
  selector: 'po-work-order-table',
  imports: [MatTableModule, MatIconModule, MatTooltipModule, RouterLink, DatePipe, PriorityBadge, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-table.html',
  styleUrl: './work-order-table.scss',
})
export class WorkOrderTable {
  readonly items = input.required<WorkOrderSummary[]>();
  readonly now = input.required<number>();

  protected readonly columns = ['priority', 'title', 'unit', 'status', 'assignee', 'age'];

  protected age(item: WorkOrderSummary): string {
    return formatAge(item.createdAtUtc, this.now());
  }

  protected isAgeing(item: WorkOrderSummary): boolean {
    return item.status !== 'Closed' && ageInDays(item.createdAtUtc, this.now()) > AGEING_DAYS;
  }
}
