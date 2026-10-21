import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api-config';
import { WorkOrderSummary } from '../work-orders/work-order.models';

/** A plant unit's open work, counted per status. */
export interface UnitWorkload {
  plantUnitId: string;
  plantUnitCode: string;
  plantUnitName: string;
  open: number;
  approved: number;
  assigned: number;
  inProgress: number;
}

/** Work a technician holds: assigned but not started, and started. */
export interface TechnicianWorkload {
  userId: string;
  displayName: string;
  assigned: number;
  inProgress: number;
}

/** GET /api/dashboard. The API caches it for up to a minute; generatedAtUtc says when it was computed. */
export interface Dashboard {
  generatedAtUtc: string;
  ageingAfterDays: number;
  needsAttention: WorkOrderSummary[];
  needsAttentionTotal: number;
  units: UnitWorkload[];
  technicians: TechnicianWorkload[];
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  readonly #http = inject(HttpClient);
  readonly #base = `${inject(API_BASE_URL)}/api`;

  get(): Observable<Dashboard> {
    return this.#http.get<Dashboard>(`${this.#base}/dashboard`);
  }
}
