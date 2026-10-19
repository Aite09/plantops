import { CurrentUser, Role } from '../../core/auth/auth.models';
import { WorkOrderDetails, WorkOrderStatus } from './work-order.models';

export interface AvailableActions {
  edit: boolean;
  approve: boolean;
  assign: boolean;
  start: boolean;
  close: boolean;
}

const MANAGERS: readonly Role[] = ['Supervisor', 'Admin'];
const ASSIGNABLE: readonly WorkOrderStatus[] = ['Approved', 'Assigned', 'InProgress'];

/**
 * Which buttons to show this user for this work order. It mirrors the API's rules (role policies, the domain's
 * state machine, the reporter-edit rule) so people only see actions that will work. It is not the security
 * boundary: the API re-checks everything and answers 403 or 409 if this ever disagrees.
 */
export function availableActions(workOrder: WorkOrderDetails, user: CurrentUser | null): AvailableActions {
  if (!user) {
    return { edit: false, approve: false, assign: false, start: false, close: false };
  }

  const isManager = user.roles.some((role) => MANAGERS.includes(role));
  const isAssignedTechnician = user.roles.includes('Technician') && workOrder.assignedToUserId === user.id;
  const isReporter = workOrder.reportedByUserId === user.id;

  return {
    edit: workOrder.status !== 'Closed' && (isManager || (isReporter && workOrder.status === 'Open')),
    approve: isManager && workOrder.status === 'Open',
    assign: isManager && ASSIGNABLE.includes(workOrder.status),
    start: isAssignedTechnician && workOrder.status === 'Assigned',
    close: isAssignedTechnician && workOrder.status === 'InProgress',
  };
}
