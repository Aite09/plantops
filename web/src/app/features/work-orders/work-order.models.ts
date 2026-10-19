// TypeScript mirrors of the API's JSON contracts (see Swagger at /swagger).

export type WorkOrderStatus = 'Open' | 'Approved' | 'Assigned' | 'InProgress' | 'Closed';
export type WorkOrderPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const STATUSES: readonly WorkOrderStatus[] = ['Open', 'Approved', 'Assigned', 'InProgress', 'Closed'];
export const PRIORITIES: readonly WorkOrderPriority[] = ['Critical', 'High', 'Medium', 'Low'];

export const STATUS_LABELS: Record<WorkOrderStatus, string> = {
  Open: 'Open',
  Approved: 'Approved',
  Assigned: 'Assigned',
  InProgress: 'In progress',
  Closed: 'Closed',
};

export interface WorkOrderSummary {
  id: string;
  title: string;
  priority: WorkOrderPriority;
  status: WorkOrderStatus;
  plantUnitCode: string;
  equipmentTag: string;
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdAtUtc: string;
}

export interface WorkOrderDetails {
  id: string;
  title: string;
  description: string;
  priority: WorkOrderPriority;
  status: WorkOrderStatus;
  plantUnitId: string;
  plantUnitCode: string;
  plantUnitName: string;
  equipmentId: string;
  equipmentTag: string;
  equipmentName: string;
  reportedByUserId: string;
  reportedByName: string | null;
  approvedByUserId: string | null;
  approvedByName: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  resolutionNotes: string | null;
  createdAtUtc: string;
  approvedAtUtc: string | null;
  assignedAtUtc: string | null;
  startedAtUtc: string | null;
  closedAtUtc: string | null;
  /** Row version to send back when editing (optimistic concurrency). */
  version: string;
}

export interface AuditChange {
  property: string;
  oldValue: unknown;
  newValue: unknown;
}

export interface AuditEntry {
  timestampUtc: string;
  action: 'Created' | 'Updated' | 'Deleted';
  userId: string | null;
  userName: string | null;
  changes: AuditChange[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Equipment {
  id: string;
  tag: string;
  name: string;
}

export interface PlantUnit {
  id: string;
  code: string;
  name: string;
  equipment: Equipment[];
}

export interface WorkOrderFilter {
  /** Leave out closed work orders (the default view). Ignored when a specific status is chosen. */
  activeOnly: boolean;
  status: WorkOrderStatus | null;
  priority: WorkOrderPriority | null;
  plantUnitId: string | null;
  assignedToUserId: string | null;
  page: number;
  pageSize: number;
}

/** What the create and edit forms produce. */
export interface WorkOrderInput {
  title: string;
  description: string;
  priority: WorkOrderPriority;
  equipmentId: string;
}
