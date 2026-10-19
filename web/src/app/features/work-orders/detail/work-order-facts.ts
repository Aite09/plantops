import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { WorkOrderDetails } from '../work-order.models';

/** Presentational: the who/when facts of a work order as a definition list. */
@Component({
  selector: 'po-work-order-facts',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let wo = workOrder();
    <dl>
      <div>
        <dt>Plant unit</dt>
        <dd><span class="mono">{{ wo.plantUnitCode }}</span> {{ wo.plantUnitName }}</dd>
      </div>
      <div>
        <dt>Equipment</dt>
        <dd><span class="mono">{{ wo.equipmentTag }}</span> {{ wo.equipmentName }}</dd>
      </div>
      <div>
        <dt>Reported</dt>
        <dd>
          {{ wo.reportedByName ?? 'Unknown user' }}
          <span class="when">{{ wo.createdAtUtc | date: 'd MMM y, HH:mm' }}</span>
        </dd>
      </div>
      @if (wo.approvedAtUtc) {
        <div>
          <dt>Approved</dt>
          <dd>
            {{ wo.approvedByName ?? 'Unknown user' }}
            <span class="when">{{ wo.approvedAtUtc | date: 'd MMM y, HH:mm' }}</span>
          </dd>
        </div>
      }
      <div>
        <dt>Assigned to</dt>
        <dd>
          @if (wo.assignedToUserId) {
            {{ wo.assignedToName ?? 'Unknown user' }}
            <span class="when">{{ wo.assignedAtUtc | date: 'd MMM y, HH:mm' }}</span>
          } @else {
            <span class="muted">Nobody yet</span>
          }
        </dd>
      </div>
      @if (wo.startedAtUtc) {
        <div>
          <dt>Started</dt>
          <dd><span class="when">{{ wo.startedAtUtc | date: 'd MMM y, HH:mm' }}</span></dd>
        </div>
      }
      @if (wo.closedAtUtc) {
        <div>
          <dt>Closed</dt>
          <dd><span class="when">{{ wo.closedAtUtc | date: 'd MMM y, HH:mm' }}</span></dd>
        </div>
      }
    </dl>
  `,
  styles: `
    dl {
      display: grid;
      gap: 14px;
      margin: 0;
      padding: 16px;
      border: 1px solid var(--po-line);
      border-radius: 8px;
      background: var(--po-panel);
    }
    dt {
      color: var(--po-ink-muted);
      font-size: 12px;
      font-weight: 500;
    }
    dd {
      margin: 2px 0 0;
    }
    .when {
      display: block;
      color: var(--po-ink-muted);
      font-size: 13px;
    }
  `,
})
export class WorkOrderFacts {
  readonly workOrder = input.required<WorkOrderDetails>();
}
