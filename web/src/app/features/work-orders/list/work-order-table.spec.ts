import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { WorkOrderSummary } from '../work-order.models';
import { WorkOrderTable } from './work-order-table';

const NOW = Date.parse('2026-03-16T06:00:00Z');

function row(id: string, createdAtUtc: string): WorkOrderSummary {
  return {
    id,
    title: `Fault ${id}`,
    priority: 'Medium',
    status: 'Open',
    plantUnitCode: 'U-100',
    equipmentTag: 'P-101A',
    assignedToUserId: null,
    assignedToName: null,
    createdAtUtc,
  };
}

describe('WorkOrderTable', () => {
  it('says "Ageing" under the title of old open work, for phones where the Age column is hidden', async () => {
    TestBed.configureTestingModule({ imports: [WorkOrderTable], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(WorkOrderTable);
    fixture.componentRef.setInput('items', [row('old', '2026-03-08T06:00:00Z'), row('new', '2026-03-15T06:00:00Z')]);
    fixture.componentRef.setInput('now', NOW);
    await fixture.whenStable();

    const titleCells = [...(fixture.nativeElement as HTMLElement).querySelectorAll('td.title-cell')];
    expect(titleCells[0].querySelector('.ageing-inline')?.textContent).toContain('Ageing · 8d');
    expect(titleCells[1].querySelector('.ageing-inline')).toBeNull();
  });
});
