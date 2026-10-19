import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AuditChange, AuditEntry, STATUS_LABELS, WorkOrderStatus } from '../work-order.models';

interface HistoryItem {
  when: string;
  who: string;
  headline: string;
  details: string[];
}

// Fields that only restate the headline (timestamps, who approved) or never change after creation.
const HIDDEN_FIELDS = new Set([
  'Status',
  'AssignedToUserId',
  'ApprovedByUserId',
  'ReportedByUserId',
  'PlantUnitId',
  'EquipmentId',
  'CreatedAtUtc',
  'ApprovedAtUtc',
  'AssignedAtUtc',
  'StartedAtUtc',
  'ClosedAtUtc',
]);

/** Presentational: the audit trail as a readable timeline, newest first. */
@Component({
  selector: 'po-work-order-history',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="timeline">
      @for (item of items(); track $index) {
        <li>
          <span class="dot" aria-hidden="true"></span>
          <div class="line">
            <span class="headline">{{ item.headline }}</span>
            <span class="muted">by {{ item.who }}</span>
          </div>
          <time class="muted when" [attr.datetime]="item.when">{{ item.when | date: 'd MMM y, HH:mm' }}</time>
          @for (detail of item.details; track $index) {
            <p class="detail">{{ detail }}</p>
          }
        </li>
      } @empty {
        <li class="muted">No recorded changes.</li>
      }
    </ol>
  `,
  styles: `
    .timeline {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      position: relative;
      padding: 0 0 18px 24px;
    }
    li:not(:last-child)::before {
      content: '';
      position: absolute;
      left: 5px;
      top: 14px;
      bottom: 0;
      width: 1px;
      background: var(--po-line);
    }
    .dot {
      position: absolute;
      left: 0;
      top: 5px;
      width: 9px;
      height: 9px;
      border: 1px solid var(--po-ink-muted);
      border-radius: 50%;
      background: var(--mat-sys-surface);
    }
    li:first-child .dot {
      border-color: var(--mat-sys-primary);
      background: var(--mat-sys-primary);
    }
    .line {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 8px;
    }
    .headline {
      font-weight: 500;
    }
    .when {
      display: block;
      font-size: 13px;
    }
    .detail {
      margin: 4px 0 0;
      max-width: 72ch;
      overflow-wrap: anywhere;
    }
  `,
})
export class WorkOrderHistory {
  readonly entries = input.required<AuditEntry[]>();
  /** Display names keyed by user id, for actors and for assignee ids inside changes. */
  readonly userNames = input<Record<string, string>>({});

  protected readonly items = computed<HistoryItem[]>(() => {
    const names = this.userNames();
    const nameOf = (id: unknown) => (typeof id === 'string' && names[id]) || 'another user';
    return [...this.entries()].reverse().map((entry) => ({
      when: entry.timestampUtc,
      who: entry.userName ?? (entry.userId ? nameOf(entry.userId) : 'the system'),
      headline: headline(entry, nameOf),
      details: entry.action === 'Created' ? [] : entry.changes.filter((c) => !HIDDEN_FIELDS.has(c.property)).map(describe),
    }));
  });
}

function headline(entry: AuditEntry, nameOf: (id: unknown) => string): string {
  if (entry.action === 'Created') return 'Reported the fault';
  if (entry.action === 'Deleted') return 'Deleted';

  const status = entry.changes.find((c) => c.property === 'Status');
  const assignee = entry.changes.find((c) => c.property === 'AssignedToUserId');

  switch (status?.newValue as WorkOrderStatus | undefined) {
    case 'Approved':
      return 'Approved';
    case 'Assigned':
      return status?.oldValue === 'InProgress'
        ? `Handed over to ${nameOf(assignee?.newValue)}`
        : `Assigned to ${nameOf(assignee?.newValue)}`;
    case 'InProgress':
      return 'Started work';
    case 'Closed':
      return 'Closed';
    default:
      return assignee ? `Reassigned to ${nameOf(assignee.newValue)}` : 'Edited details';
  }
}

function describe(change: AuditChange): string {
  switch (change.property) {
    case 'Description':
      return 'Updated the fault description.';
    case 'ResolutionNotes':
      return `Resolution: ${String(change.newValue ?? '')}`;
    case 'Title':
      return `Title: “${String(change.oldValue)}” → “${String(change.newValue)}”`;
    case 'Priority':
      return `Priority: ${String(change.oldValue)} → ${String(change.newValue)}`;
    default:
      return `${change.property}: ${label(change.oldValue)} → ${label(change.newValue)}`;
  }
}

function label(value: unknown): string {
  if (value === null || value === undefined) return 'none';
  return STATUS_LABELS[value as WorkOrderStatus] ?? String(value);
}
