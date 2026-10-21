import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, Subject } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth-store';
import { Features, FeaturesApi } from '../../../core/features-api';
import { UsersApi } from '../../users/users-api';
import { FaultExplanation, WorkOrderDetails } from '../work-order.models';
import { WorkOrdersApi } from '../work-orders-api';
import { WorkOrderDetailPage } from './work-order-detail-page';

const WORK_ORDER: WorkOrderDetails = {
  id: 'wo-1',
  title: 'Mechanical seal leaking',
  description: 'Steady drip from the inboard seal.',
  priority: 'High',
  status: 'Open',
  plantUnitId: 'u-100',
  plantUnitCode: 'U-100',
  plantUnitName: 'Crude Distillation',
  equipmentId: 'e-1',
  equipmentTag: 'P-101A',
  equipmentName: 'Crude charge pump A',
  reportedByUserId: 'u-staff',
  reportedByName: 'Sam Staff',
  approvedByUserId: null,
  approvedByName: null,
  assignedToUserId: null,
  assignedToName: null,
  resolutionNotes: null,
  createdAtUtc: '2026-03-16T04:00:00Z',
  approvedAtUtc: null,
  assignedAtUtc: null,
  startedAtUtc: null,
  closedAtUtc: null,
  version: 'AAAAAAAAB9E=',
};

/** Stands in for WorkOrdersApi: the page only loads one work order, its history, and asks for explanations here. */
class FakeWorkOrdersApi {
  readonly answers = new Subject<FaultExplanation>();
  explainCalls = 0;

  get(): Observable<WorkOrderDetails> {
    return of(WORK_ORDER);
  }

  history(): Observable<[]> {
    return of([]);
  }

  explain(): Observable<FaultExplanation> {
    this.explainCalls++;
    return this.answers;
  }
}

describe('WorkOrderDetailPage explain section', () => {
  let api: FakeWorkOrdersApi;
  let fixture: ComponentFixture<WorkOrderDetailPage>;

  async function render(features: Features): Promise<HTMLElement> {
    api = new FakeWorkOrdersApi();
    TestBed.configureTestingModule({
      imports: [WorkOrderDetailPage],
      providers: [
        provideRouter([]),
        { provide: WorkOrdersApi, useValue: api },
        { provide: FeaturesApi, useValue: { get: () => of(features) } },
        { provide: UsersApi, useValue: { list: () => of([]) } },
        { provide: AuthStore, useValue: { user: signal(null) } },
      ],
    });
    fixture = TestBed.createComponent(WorkOrderDetailPage);
    fixture.componentRef.setInput('id', 'wo-1');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('is not there at all when the API has the feature switched off', async () => {
    const page = await render({ explainFault: false });

    expect(page.querySelector('h1')?.textContent).toContain('Mechanical seal leaking');
    expect(page.querySelector('po-fault-explanation')).toBeNull();
  });

  it('asks the API once, however often the button is pressed while waiting, then shows the answer', async () => {
    const page = await render({ explainFault: true });
    const button = page.querySelector('po-fault-explanation button') as HTMLButtonElement;

    button.click();
    await fixture.whenStable();
    button.click();
    api.answers.next({ text: 'Possible causes:\n- Worn seal faces', model: 'phi4-mini', generatedAtUtc: '2026-03-16T06:00:00Z' });
    await fixture.whenStable();

    expect(api.explainCalls).toBe(1);
    expect(page.querySelector('po-fault-explanation .text')?.textContent).toContain('Worn seal faces');
  });

  it('stops waiting for the answer when the user leaves the page', async () => {
    const page = await render({ explainFault: true });
    (page.querySelector('po-fault-explanation button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.answers.observed).toBe(true);

    fixture.destroy();

    expect(api.answers.observed).toBe(false);
  });
});

@Component({ template: '' })
class BlankPage {}

describe('WorkOrderDetailPage back link', () => {
  async function arriveFrom(previousUrl: string): Promise<HTMLAnchorElement> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'dashboard', component: BlankPage },
            { path: 'work-orders', component: BlankPage },
            { path: 'work-orders/:id', component: WorkOrderDetailPage },
          ],
          withComponentInputBinding(),
        ),
        { provide: WorkOrdersApi, useValue: new FakeWorkOrdersApi() },
        { provide: FeaturesApi, useValue: { get: () => of({ explainFault: false }) } },
        { provide: UsersApi, useValue: { list: () => of([]) } },
        { provide: AuthStore, useValue: { user: signal(null) } },
      ],
    });
    const harness = await RouterTestingHarness.create(previousUrl);
    await harness.navigateByUrl('/work-orders/wo-1', WorkOrderDetailPage);
    return harness.routeNativeElement?.querySelector('a.back') as HTMLAnchorElement;
  }

  it('goes back to the dashboard when that is where the user came from', async () => {
    const back = await arriveFrom('/dashboard');

    expect(back.textContent).toContain('Dashboard');
    expect(back.getAttribute('href')).toBe('/dashboard');
  });

  it('goes back to the work-order list from anywhere else', async () => {
    const back = await arriveFrom('/work-orders?status=Open');

    expect(back.textContent).toContain('Work orders');
    expect(back.getAttribute('href')).toBe('/work-orders');
  });
});
