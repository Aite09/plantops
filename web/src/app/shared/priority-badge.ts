import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { WorkOrderPriority } from '../features/work-orders/work-order.models';

const ICONS: Record<WorkOrderPriority, string> = {
  Critical: 'error',
  High: 'keyboard_double_arrow_up',
  Medium: 'drag_handle',
  Low: 'keyboard_arrow_down',
};

/** Priority as icon + word. Colour only for Critical and High, so they stand out; never colour alone. */
@Component({
  selector: 'po-priority-badge',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<mat-icon aria-hidden="true">{{ icon() }}</mat-icon><span class="label">{{ priority() }}</span>`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px 2px 4px;
      border-radius: 999px;
      font-weight: 500;
      line-height: 20px;
      white-space: nowrap;
      color: var(--po-ink-muted);
    }
    mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
    :host(.critical) {
      color: var(--po-critical-ink);
      background: var(--po-critical-surface);
    }
    :host(.high) {
      color: var(--po-high-ink);
      background: var(--po-high-surface);
    }
    :host(.medium) {
      color: var(--po-ink);
    }
    /* Phones: the icon carries the priority (each level has its own shape), the word stays for screen readers. */
    @media (max-width: 600px) {
      :host {
        padding: 2px 4px;
      }
      .label {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip-path: inset(50%);
        white-space: nowrap;
      }
    }
  `,
  host: { '[class]': 'cssClass()' },
})
export class PriorityBadge {
  readonly priority = input.required<WorkOrderPriority>();
  protected readonly icon = computed(() => ICONS[this.priority()]);
  protected readonly cssClass = computed(() => this.priority().toLowerCase());
}
