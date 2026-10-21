import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, Subject, throwError } from 'rxjs';
import { WorkOrderSummary } from '../work-orders/work-order.models';
import { Dashboard, DashboardApi } from './dashboard-api';
import { DashboardPage } from './dashboard-page';

/** Stands in for DashboardApi: answers with whatever each test queues, and counts the requests. */
class FakeDashboardApi {
  responses: Observable<Dashboard>[] = [];
  calls = 0;

  get(): Observable<Dashboard> {
    this.calls++;
    return this.responses.shift() ?? of(dashboard());
  }
}

function row(id: string, title: string): WorkOrderSummary {
  return {
    id,
    title,
    priority: 'Critical',
    status: 'Open',
    plantUnitCode: 'U-300',
    equipmentTag: 'CT-301',
    assignedToUserId: null,
    assignedToName: null,
    createdAtUtc: '2026-03-16T04:00:00Z',
  };
}

function dashboard(overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    generatedAtUtc: '2026-03-16T06:00:00Z',
    ageingAfterDays: 7,
    needsAttention: [row('wo-1', 'Fan vibration alarm'), row('wo-2', 'Burner flame scanner fault')],
    needsAttentionTotal: 2,
    units: [],
    technicians: [],
    ...overrides,
  };
}

describe('DashboardPage', () => {
  let api: FakeDashboardApi;
  let fixture: ComponentFixture<DashboardPage>;

  async function render(): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [provideRouter([]), { provide: DashboardApi, useValue: api }],
    });
    fixture = TestBed.createComponent(DashboardPage);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => (api = new FakeDashboardApi()));

  it('lists the work that needs attention and says when the numbers were computed', async () => {
    const page = await render();

    const titles = [...page.querySelectorAll('.row-link')].map((link) => link.textContent?.trim());
    expect(titles).toEqual(['Fan vibration alarm', 'Burner flame scanner fault']);
    expect(page.querySelector('.summary')?.textContent).toContain('2 need attention');
    expect(page.querySelector('.summary time')?.getAttribute('datetime')).toBe('2026-03-16T06:00:00Z');
  });

  it('says plainly when nothing needs attention, with the ageing rule it used', async () => {
    api.responses = [of(dashboard({ needsAttention: [], needsAttentionTotal: 0 }))];

    const page = await render();

    expect(page.querySelector('.state')?.textContent).toContain('Nothing needs attention');
    expect(page.querySelector('.state')?.textContent).toContain('older than 7 days');
    expect(page.querySelector('po-work-order-table')).toBeNull();
  });

  it('says how many rows are left out when the list is cut short', async () => {
    api.responses = [of(dashboard({ needsAttentionTotal: 31 }))];

    const page = await render();

    expect(page.querySelector('.more')?.textContent).toContain('Showing 2 of 31');
  });

  it('shows why loading failed, and Try again asks the API once more with focus kept on the button', async () => {
    api.responses = [throwError(() => new HttpErrorResponse({ status: 0 }))];
    const page = await render();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Cannot reach the PlantOps server');
    const button = refreshButton(page);
    expect(button.textContent).toContain('Try again');
    button.focus();
    button.click();
    await fixture.whenStable();

    expect(api.calls).toBe(2);
    expect(page.querySelector('[role="alert"]')).toBeNull();
    expect(page.querySelectorAll('.row-link')).toHaveLength(2);
    expect(document.activeElement).toBe(button);
  });

  it('keeps the numbers on screen when a refresh fails, says why, and keeps focus on Refresh', async () => {
    const page = await render();
    api.responses = [throwError(() => new HttpErrorResponse({ status: 0 }))];
    const button = refreshButton(page);

    button.focus();
    button.click();
    await fixture.whenStable();

    expect(page.querySelectorAll('.row-link')).toHaveLength(2);
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Could not refresh: Cannot reach the PlantOps server');
    expect(document.activeElement).toBe(button);
    expect(refreshButton(page)).toBe(button);
  });

  it('ignores Refresh while a refresh is still running', async () => {
    const page = await render();
    const pending = new Subject<Dashboard>();
    api.responses = [pending];

    refreshButton(page).click();
    await fixture.whenStable();
    refreshButton(page).click();
    await fixture.whenStable();

    expect(api.calls).toBe(2);
    expect(refreshButton(page).textContent).toContain('Refreshing');
    pending.next(dashboard());
    pending.complete();
  });

  it('asks again when the browser tab comes back into view', async () => {
    await render();

    document.dispatchEvent(new Event('visibilitychange'));
    await fixture.whenStable();

    expect(document.visibilityState).toBe('visible');
    expect(api.calls).toBe(2);
  });

  function refreshButton(page: HTMLElement): HTMLButtonElement {
    return page.querySelector('.page-header button') as HTMLButtonElement;
  }
});
