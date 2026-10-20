import { CurrentUser, Role } from '../../core/auth/auth.models';
import { AvailableActions, availableActions } from './work-order-actions';
import { STATUSES, WorkOrderDetails, WorkOrderStatus } from './work-order.models';

type Action = keyof AvailableActions;
type Viewer =
  | 'anonymous'
  | 'staffReporter'
  | 'otherStaff'
  | 'supervisor'
  | 'admin'
  | 'assignedTechnician'
  | 'otherTechnician';

const ACTIONS: readonly Action[] = ['edit', 'approve', 'assign', 'start', 'close'];

const REPORTER_ID = 'user-staff-reporter';
const ASSIGNEE_ID = 'user-tech-assigned';

const VIEWERS: Record<Viewer, CurrentUser | null> = {
  anonymous: null,
  staffReporter: user(REPORTER_ID, 'Staff'),
  otherStaff: user('user-staff-other', 'Staff'),
  supervisor: user('user-supervisor', 'Supervisor'),
  admin: user('user-admin', 'Admin'),
  assignedTechnician: user(ASSIGNEE_ID, 'Technician'),
  otherTechnician: user('user-tech-other', 'Technician'),
};

// Rows: who is looking. Columns: the work order's status. Each cell lists the buttons that viewer should get.
// The work order was reported by staffReporter and, from Assigned onwards, is assigned to assignedTechnician.
// Managers can still assign while Assigned or InProgress (reassigning, e.g. at shift handover); Closed allows nothing.
const EXPECTED: Record<Viewer, Record<WorkOrderStatus, Action[]>> = {
  anonymous: { Open: [], Approved: [], Assigned: [], InProgress: [], Closed: [] },
  staffReporter: { Open: ['edit'], Approved: [], Assigned: [], InProgress: [], Closed: [] },
  otherStaff: { Open: [], Approved: [], Assigned: [], InProgress: [], Closed: [] },
  supervisor: {
    Open: ['edit', 'approve'],
    Approved: ['edit', 'assign'],
    Assigned: ['edit', 'assign'],
    InProgress: ['edit', 'assign'],
    Closed: [],
  },
  admin: {
    Open: ['edit', 'approve'],
    Approved: ['edit', 'assign'],
    Assigned: ['edit', 'assign'],
    InProgress: ['edit', 'assign'],
    Closed: [],
  },
  assignedTechnician: { Open: [], Approved: [], Assigned: ['start'], InProgress: ['close'], Closed: [] },
  otherTechnician: { Open: [], Approved: [], Assigned: [], InProgress: [], Closed: [] },
};

const CASES = (Object.keys(EXPECTED) as Viewer[]).flatMap((viewer) =>
  STATUSES.map((status) => ({ viewer, status, allowed: EXPECTED[viewer][status] })),
);

describe('availableActions', () => {
  it.each(CASES)('$status work order seen by $viewer offers $allowed', ({ viewer, status, allowed }) => {
    const actions = availableActions(workOrder(status), VIEWERS[viewer]);

    expect(allowedActions(actions)).toEqual(allowed);
  });

  it('does not let a user without the Technician role start or close work assigned to their id', () => {
    // The assignee lost the Technician role after being assigned; the API's PerformWork policy would reject them.
    const formerTechnician = user(ASSIGNEE_ID, 'Staff');

    const assigned = availableActions(workOrder('Assigned'), formerTechnician);
    const inProgress = availableActions(workOrder('InProgress'), formerTechnician);

    expect(assigned.start).toBe(false);
    expect(inProgress.close).toBe(false);
  });
});

function allowedActions(actions: AvailableActions): Action[] {
  return ACTIONS.filter((action) => actions[action]);
}

function user(id: string, role: Role): CurrentUser {
  return { id, email: `${id}@plantops.test`, displayName: id, roles: [role] };
}

function workOrder(status: WorkOrderStatus): WorkOrderDetails {
  const assigned = status === 'Assigned' || status === 'InProgress' || status === 'Closed';
  return {
    id: 'wo-1',
    title: 'Pump P-101 leaking at the seal',
    description: 'Steady drip from the mechanical seal.',
    priority: 'High',
    status,
    plantUnitId: 'unit-1',
    plantUnitCode: 'CDU-1',
    plantUnitName: 'Crude distillation unit',
    equipmentId: 'eq-1',
    equipmentTag: 'P-101',
    equipmentName: 'Crude charge pump',
    reportedByUserId: REPORTER_ID,
    reportedByName: 'Sam Staff',
    approvedByUserId: status === 'Open' ? null : 'user-supervisor',
    approvedByName: status === 'Open' ? null : 'Pat Supervisor',
    assignedToUserId: assigned ? ASSIGNEE_ID : null,
    assignedToName: assigned ? 'Alex Technician' : null,
    resolutionNotes: status === 'Closed' ? 'Replaced the seal.' : null,
    createdAtUtc: '2026-03-02T08:00:00Z',
    approvedAtUtc: status === 'Open' ? null : '2026-03-02T08:30:00Z',
    assignedAtUtc: assigned ? '2026-03-02T09:00:00Z' : null,
    startedAtUtc: status === 'InProgress' || status === 'Closed' ? '2026-03-02T09:15:00Z' : null,
    closedAtUtc: status === 'Closed' ? '2026-03-02T11:00:00Z' : null,
    version: 'AAAAAAAAB9E=',
  };
}
