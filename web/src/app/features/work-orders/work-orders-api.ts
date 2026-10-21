import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { API_BASE_URL } from '../../core/api-config';
import {
  AuditEntry,
  FaultExplanation,
  PagedResult,
  PlantUnit,
  WorkOrderDetails,
  WorkOrderFilter,
  WorkOrderInput,
  WorkOrderSummary,
} from './work-order.models';

/** Typed client for /api/work-orders and /api/plant-units. Components never build URLs themselves. */
@Injectable({ providedIn: 'root' })
export class WorkOrdersApi {
  readonly #http = inject(HttpClient);
  readonly #base = `${inject(API_BASE_URL)}/api`;

  // Plant units and equipment are reference data: fetched once per session and shared by every subscriber.
  readonly #plantUnits$ = this.#http.get<PlantUnit[]>(`${this.#base}/plant-units`).pipe(shareReplay(1));

  list(filter: WorkOrderFilter): Observable<PagedResult<WorkOrderSummary>> {
    let params = new HttpParams().set('page', filter.page).set('pageSize', filter.pageSize);
    if (filter.status) params = params.set('status', filter.status);
    else if (filter.activeOnly) params = params.set('activeOnly', true);
    if (filter.priority) params = params.set('priority', filter.priority);
    if (filter.plantUnitId) params = params.set('plantUnitId', filter.plantUnitId);
    if (filter.assignedToUserId) params = params.set('assignedToUserId', filter.assignedToUserId);
    return this.#http.get<PagedResult<WorkOrderSummary>>(`${this.#base}/work-orders`, { params });
  }

  get(id: string): Observable<WorkOrderDetails> {
    return this.#http.get<WorkOrderDetails>(`${this.#base}/work-orders/${encodeURIComponent(id)}`);
  }

  history(id: string): Observable<AuditEntry[]> {
    return this.#http.get<AuditEntry[]>(`${this.#base}/work-orders/${encodeURIComponent(id)}/history`);
  }

  create(input: WorkOrderInput): Observable<{ id: string }> {
    return this.#http.post<{ id: string }>(`${this.#base}/work-orders`, input);
  }

  update(id: string, input: Omit<WorkOrderInput, 'equipmentId'>, version: string): Observable<void> {
    return this.#http.put<void>(`${this.#base}/work-orders/${encodeURIComponent(id)}`, { ...input, version });
  }

  approve(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#base}/work-orders/${encodeURIComponent(id)}/approve`, null);
  }

  assign(id: string, technicianId: string): Observable<void> {
    return this.#http.post<void>(`${this.#base}/work-orders/${encodeURIComponent(id)}/assign`, { technicianId });
  }

  start(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#base}/work-orders/${encodeURIComponent(id)}/start`, null);
  }

  close(id: string, resolutionNotes: string): Observable<void> {
    return this.#http.post<void>(`${this.#base}/work-orders/${encodeURIComponent(id)}/close`, { resolutionNotes });
  }

  /** Asks the API's AI model about this fault: slow (seconds), rate-limited per user, and nothing is saved. */
  explain(id: string): Observable<FaultExplanation> {
    return this.#http.post<FaultExplanation>(`${this.#base}/work-orders/${encodeURIComponent(id)}/explanation`, null);
  }

  plantUnits(): Observable<PlantUnit[]> {
    return this.#plantUnits$;
  }
}
