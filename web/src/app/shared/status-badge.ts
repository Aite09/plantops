import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { STATUS_LABELS, WorkOrderStatus } from '../features/work-orders/work-order.models';

const ICONS: Record<WorkOrderStatus, string> = {
  Open: 'radio_button_unchecked',
  Approved: 'task_alt',
  Assigned: 'assignment_ind',
  InProgress: 'build',
  Closed: 'check_circle',
};

/** Status as icon + word in neutral ink: status is normal information, not an alarm. */
@Component({
  selector: 'po-status-badge',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<mat-icon aria-hidden="true">{{ icon() }}</mat-icon><span>{{ label() }}</span>`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 1px 8px 1px 4px;
      border: 1px solid var(--po-line);
      border-radius: 999px;
      line-height: 20px;
      white-space: nowrap;
      color: var(--po-ink);
    }
    mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
      color: var(--po-ink-muted);
    }
    :host(.inprogress) mat-icon {
      color: var(--mat-sys-primary);
    }
    :host(.closed) {
      color: var(--po-ink-muted);
    }
    :host(.closed) mat-icon {
      color: var(--po-ok-ink);
    }
  `,
  host: { '[class]': 'cssClass()' },
})
export class StatusBadge {
  readonly status = input.required<WorkOrderStatus>();
  protected readonly icon = computed(() => ICONS[this.status()]);
  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
  protected readonly cssClass = computed(() => this.status().toLowerCase());
}
