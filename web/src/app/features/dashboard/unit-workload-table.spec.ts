import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { UnitWorkload } from './dashboard-api';
import { UnitWorkloadTable } from './unit-workload-table';

const UNITS: UnitWorkload[] = [
  { plantUnitId: 'u-100', plantUnitCode: 'U-100', plantUnitName: 'Crude Distillation', open: 2, approved: 0, assigned: 1, inProgress: 0 },
  { plantUnitId: 'u-300', plantUnitCode: 'U-300', plantUnitName: 'Cooling Water System', open: 1, approved: 1, assigned: 0, inProgress: 0 },
];

describe('UnitWorkloadTable', () => {
  async function render(units: UnitWorkload[]): Promise<HTMLElement> {
    TestBed.configureTestingModule({ imports: [UnitWorkloadTable], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(UnitWorkloadTable);
    fixture.componentRef.setInput('units', units);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function cells(row: Element | null | undefined): HTMLElement[] {
    return [...(row?.querySelectorAll('td') ?? [])] as HTMLElement[];
  }

  it('links each non-zero count to the list filtered by that unit and status, named for screen readers', async () => {
    const table = await render(UNITS);

    const open = cells(table.querySelector('tbody tr'))[0].querySelector('a');
    expect(open?.getAttribute('href')).toBe('/work-orders?unit=u-100&status=Open');
    expect(open?.textContent?.replace(/\s+/g, ' ').trim()).toBe('2 Open on U-100');
  });

  it('shows zeros as quiet text, not links', async () => {
    const table = await render(UNITS);

    const approved = cells(table.querySelector('tbody tr'))[1];
    expect(approved.querySelector('a')).toBeNull();
    expect(approved.querySelector('.zero')?.textContent).toBe('0');
  });

  it('totals each unit and each status, and the totals link to the matching list', async () => {
    const table = await render(UNITS);

    expect(cells(table.querySelector('tbody tr'))[4].querySelector('a')?.getAttribute('href')).toBe('/work-orders?unit=u-100');
    const footer = cells(table.querySelector('tfoot tr')).map((cell) => cell.textContent?.replace(/\s+/g, ' ').trim());
    expect(footer).toEqual(['3 Open in all units', '1 Approved in all units', '1 Assigned in all units', '0', '5 open in all units']);
  });
});
